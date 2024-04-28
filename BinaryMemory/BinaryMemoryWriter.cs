using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BinaryMemory
{
    /// <summary>
    /// A writer that writes to a region of memory with a fixed length.
    /// </summary>
    public class BinaryMemoryWriter
    {
        /// <summary>
        /// The underlying memory.
        /// </summary>
        private readonly Memory<byte> _memory;

        /// <summary>
        /// The reservations for values.
        /// </summary>
        private readonly Dictionary<string, int> _reservations;

        /// <summary>
        /// The current position of the writer.
        /// </summary>
        // Placed in a field so that range checking can happen in the property.
        private int _position;

        /// <summary>
        /// Whether or not to read in big endian byte ordering.
        /// </summary>
        public bool BigEndian { get; set; }

        /// <summary>
        /// The current position of the writer.
        /// </summary>
        public int Position
        {
            get => _position;
            set
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)value, (uint)Length, nameof(value));

                _position = value;
            }
        }

        /// <summary>
        /// The length of the underlying memory.
        /// </summary>
        public int Length => _memory.Length;

        /// <summary>
        /// The remaining length starting from the current position.
        /// </summary>
        public int Remaining => Length - Position;

        /// <summary>
        /// Create a <see cref="BinaryMemoryWriter"/> over a region of memory.
        /// </summary>
        /// <param name="memory">A region of memory.</param>
        /// <param name="bigEndian">Whether or not to write in big endian byte ordering.</param>
        public BinaryMemoryWriter(Memory<byte> memory, bool bigEndian = false)
        {
            _memory = memory;
            _reservations = [];
            BigEndian = bigEndian;
        }

        #region Stream

        public Stream ToStream()
            => new MemoryStream(_memory.ToArray(), true);

        public Stream GetStream(int length)
            => new MemoryStream(_memory.Slice(_position, length).ToArray(), true);

        public Stream GetStream(int position, int length)
            => new MemoryStream(_memory.Slice(position, length).ToArray(), true);

        #endregion

        #region Reservation

        private void Reserve(string name, string typeName, int length)
        {
            name = $"{name}:{typeName}";
            if (_reservations.ContainsKey(name))
            {
                throw new ArgumentException($"Key already reserved: {name}", nameof(name));
            }

            _reservations[name] = _position;
            for (int i = 0; i < length; i++)
            {
                Write(0xFE);
            }
        }

        private int Fill(string name, string typeName)
        {
            name = $"{name}:{typeName}";
            if (!_reservations.TryGetValue(name, out int jump))
            {
                throw new ArgumentException($"Key is not reserved: {name}", nameof(name));
            }

            _reservations.Remove(name);
            return jump;
        }

        #endregion

        #region Position

        public void Advance(int count) => Position += count;

        public void Rewind(int count) => Position -= count;

        public void Reset() => _position = 0;

        public void Align(int alignment)
        {
            int remainder = _position % alignment;
            if (remainder > 0)
            {
                Position += alignment - remainder;
            }
        }

        public void AlignFrom(int position, int alignment)
        {
            int remainder = position % alignment;
            if (remainder > 0)
            {
                position += alignment - remainder;
            }
            Position = position;
        }

        #endregion

        #region Write Helpers

        private void WriteEndian<T>(T value, Func<T, T> reverseEndianness) where T : unmanaged
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                value = reverseEndianness(value);
            }

            Write(value);
        }

        private void WriteArray<T>(IList<T> values) where T : unmanaged
        {
            foreach (T value in values)
            {
                Write(value);
            }
        }

        private void WriteArrayEndian<T>(IList<T> values, Func<T, T> reverseEndianness) where T : unmanaged
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                foreach (T value in values)
                {
                    Write(reverseEndianness(value));
                }
            }
            else
            {
                foreach (T value in values)
                {
                    Write(value);
                }
            }
        }

        private void WriteArrayConvertEndian<TFrom, TTo>(IList<TFrom> values, Func<TFrom, TTo> convert, Func<TTo, TTo> reverseEndianness)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                foreach (TFrom value in values)
                {
                    Write(reverseEndianness(convert(value)));
                }
            }
            else
            {
                foreach (TFrom value in values)
                {
                    Write(value);
                }
            }
        }

        #endregion

        #region Write

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            int size = sizeof(T);
            int endPosition = _position + size;
            if (endPosition > Length)
            {
                throw new InvalidOperationException("Cannot write beyond the specified region of memory.");
            }

            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_memory.Span), _position), value);
            _position = endPosition;
        }

        public unsafe void Reserve<T>(string name) where T : unmanaged
            => Reserve(name, "unmanaged", sizeof(T));

        public void Fill<T>(string name, T value) where T : unmanaged
        {
            int currentPositon = _position;
            _position = Fill(name, "unmanaged");
            Write(value);
            _position = currentPositon;
        }

        #endregion

        #region SByte

        public void WriteSByte(sbyte value)
            => Write(value);

        public void WriteSBytes(IList<sbyte> values)
            => WriteArray(values);

        public void ReserveSByte(string name)
            => Reserve(name, "SByte", 1);

        public void FillSByte(string name, sbyte value)
        {
            int currentPositon = _position;
            _position = Fill(name, "SByte");
            WriteSByte(value);
            _position = currentPositon;
        }

        #endregion

        #region Byte

        public void WriteByte(byte value)
            => Write(value);

        public void WriteBytes(IList<byte> values)
            => WriteArray(values);

        public void ReserveByte(string name)
            => Reserve(name, "Byte", 1);

        public void FillByte(string name, byte value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Byte");
            WriteByte(value);
            _position = currentPositon;
        }

        #endregion

        #region Int16

        public void WriteInt16(short value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteInt16s(IList<short> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveInt16(string name)
            => Reserve(name, "Int16", 2);

        public void FillInt16(string name, short value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Int16");
            WriteInt16(value);
            _position = currentPositon;
        }

        #endregion

        #region UInt16

        public void WriteUInt16(ushort value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteUInt16s(IList<ushort> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveUInt16(string name)
            => Reserve(name, "UInt16", 2);

        public void FillUInt16(string name, ushort value)
        {
            int currentPositon = _position;
            _position = Fill(name, "UInt16");
            WriteUInt16(value);
            _position = currentPositon;
        }

        #endregion

        #region Int32

        public void WriteInt32(int value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteInt32s(IList<int> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveInt32(string name)
            => Reserve(name, "Int32", 4);

        public void FillInt32(string name, int value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Int32");
            WriteInt32(value);
            _position = currentPositon;
        }

        #endregion

        #region UInt32

        public void WriteUInt32(uint value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteUInt32s(IList<uint> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveUInt32(string name)
            => Reserve(name, "UInt32", 4);

        public void FillUInt32(string name, uint value)
        {
            int currentPositon = _position;
            _position = Fill(name, "UInt32");
            WriteUInt32(value);
            _position = currentPositon;
        }

        #endregion

        #region Int64

        public void WriteInt64(long value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteInt64s(IList<long> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveInt64(string name)
            => Reserve(name, "Int64", 8);

        public void FillInt64(string name, long value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Int64");
            WriteInt64(value);
            _position = currentPositon;
        }

        #endregion

        #region UInt64

        public void WriteUInt64(ulong value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteUInt64s(IList<ulong> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveUInt64(string name)
            => Reserve(name, "UInt64", 8);

        public void FillUInt64(string name, ulong value)
        {
            int currentPositon = _position;
            _position = Fill(name, "UInt64");
            WriteUInt64(value);
            _position = currentPositon;
        }

        #endregion

        #region Int128

        public void WriteInt128(Int128 value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteInt128s(IList<Int128> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveInt128(string name)
            => Reserve(name, "Int128", 16);

        public void FillInt128(string name, Int128 value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Int128");
            WriteInt128(value);
            _position = currentPositon;
        }

        #endregion

        #region UInt128

        public void WriteUInt128(UInt128 value)
            => WriteEndian(value, BinaryPrimitives.ReverseEndianness);

        public void WriteUInt128s(IList<UInt128> values)
            => WriteArrayEndian(values, BinaryPrimitives.ReverseEndianness);

        public void ReserveUInt128(string name)
            => Reserve(name, "UInt128", 16);

        public void FillUInt128(string name, UInt128 value)
        {
            int currentPositon = _position;
            _position = Fill(name, "UInt128");
            WriteUInt128(value);
            _position = currentPositon;
        }

        #endregion

        #region Half

        public void WriteHalf(Half value)
            => WriteEndian(BitConverter.HalfToUInt16Bits(value), BinaryPrimitives.ReverseEndianness);

        public void WriteHalfs(IList<Half> values)
            => WriteArrayConvertEndian(values, BitConverter.HalfToUInt16Bits, BinaryPrimitives.ReverseEndianness);

        public void ReserveHalf(string name)
            => Reserve(name, "Half", 2);

        public void FillHalf(string name, Half value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Half");
            WriteHalf(value);
            _position = currentPositon;
        }

        #endregion

        #region Single

        public void WriteSingle(float value)
            => WriteEndian(BitConverter.SingleToUInt32Bits(value), BinaryPrimitives.ReverseEndianness);

        public void WriteSingles(IList<float> values)
            => WriteArrayConvertEndian(values, BitConverter.SingleToUInt32Bits, BinaryPrimitives.ReverseEndianness);

        public void ReserveSingle(string name)
            => Reserve(name, "Single", 4);

        public void FillSingle(string name, float value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Single");
            WriteSingle(value);
            _position = currentPositon;
        }

        #endregion

        #region Double

        public void WriteDouble(double value)
            => WriteEndian(BitConverter.DoubleToUInt64Bits(value), BinaryPrimitives.ReverseEndianness);

        public void WriteDoubles(IList<double> values)
            => WriteArrayConvertEndian(values, BitConverter.DoubleToUInt64Bits, BinaryPrimitives.ReverseEndianness);

        public void ReserveDouble(string name)
            => Reserve(name, "Double", 8);

        public void FillDouble(string name, double value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Double");
            WriteDouble(value);
            _position = currentPositon;
        }

        #endregion

        #region Boolean

        public void WriteBoolean(bool value)
            => Write((byte)(value ? 1 : 0));

        public void WriteBooleans(IList<bool> values)
        {
            foreach (bool value in values)
            {
                WriteBoolean(value);
            }
        }

        public void ReserveBoolean(string name)
            => Reserve(name, "Boolean", 1);

        public void FillBoolean(string name, bool value)
        {
            int currentPositon = _position;
            _position = Fill(name, "Boolean");
            WriteBoolean(value);
            _position = currentPositon;
        }

        #endregion

        #region String

        public void WriteString(string value, Encoding encoding, bool terminate = false)
            => WriteBytes(encoding.GetBytes(terminate ? value + '\0' : value));

        public void WriteFixedString(string value, Encoding encoding, int length, byte paddingValue = 0)
        {
            byte[] bytes = new byte[length];
            if (paddingValue != 0)
            {
                for (int i = 0; i < length; i++)
                {
                    bytes[i] = paddingValue;
                }
            }

            byte[] valueBytes = encoding.GetBytes(value);
            Array.Copy(valueBytes, bytes, Math.Min(length, valueBytes.Length));
            WriteBytes(bytes);
        }

        public void WriteUTF8(string value, bool terminate = false)
            => WriteString(value, Encoding.UTF8, terminate);

        public void WriteASCII(string value, bool terminate = false)
            => WriteString(value, Encoding.ASCII, terminate);

        public void WriteShiftJIS(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.ShiftJIS, terminate);

        public void WriteEucJP(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.EucJP, terminate);

        public void WriteEucCN(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.EucCN, terminate);

        public void WriteEucKR(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.EucKR, terminate);

        public void WriteUTF16(string value, bool terminate = false)
            => WriteString(value, BigEndian ? EncodingHelper.UTF16BE : EncodingHelper.UTF16LE, terminate);

        public void WriteUTF16LittleEndian(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.UTF16LE, terminate);

        public void WriteUTF16BigEndian(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.UTF16BE, terminate);

        public void WriteUTF32(string value, bool terminate = false)
            => WriteString(value, BigEndian ? EncodingHelper.UTF32BE : EncodingHelper.UTF32LE, terminate);

        public void WriteUTF32LittleEndian(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.UTF32LE, terminate);

        public void WriteUTF32BigEndian(string value, bool terminate = false)
            => WriteString(value, EncodingHelper.UTF32BE, terminate);

        public void WriteFixedUTF8(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, Encoding.UTF8, length, paddingValue);

        public void WriteFixedASCII(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, Encoding.ASCII, length, paddingValue);

        public void WriteFixedShiftJIS(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.ShiftJIS, length, paddingValue);

        public void WriteFixedEucJP(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.EucJP, length, paddingValue);

        public void WriteFixedEucCN(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.EucCN, length, paddingValue);

        public void WriteFixedEucKR(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.EucKR, length, paddingValue);

        public void WriteFixedUTF16(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, BigEndian ? EncodingHelper.UTF16BE : EncodingHelper.UTF16LE, length, paddingValue);

        public void WriteFixedUTF16LittleEndian(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.UTF16LE, length, paddingValue);

        public void WriteFixedUTF16BigEndian(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.UTF16BE, length, paddingValue);

        public void WriteFixedUTF32(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, BigEndian ? EncodingHelper.UTF32BE : EncodingHelper.UTF32LE, length, paddingValue);

        public void WriteFixedUTF32LittleEndian(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.UTF32LE, length, paddingValue);

        public void WriteFixedUTF32BigEndian(string value, int length, byte paddingValue = 0)
            => WriteFixedString(value, EncodingHelper.UTF32BE, length, paddingValue);

        #endregion
    }
}
