﻿using System.Buffers.Binary;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BinaryMemory
{
    /// <summary>
    /// A reader for data present in a region of memory.
    /// </summary>
    public class BinaryMemoryReader
    {
        /// <summary>
        /// Steps into positions.
        /// </summary>
        private readonly Stack<int> _steps;

        /// <summary>
        /// The underlying memory.
        /// </summary>
        private readonly Memory<byte> _memory;

        /// <summary>
        /// The current position of the reader.
        /// </summary>
        // Placed in a field so that range checking can happen in the property.
        private int _position;

        /// <summary>
        /// Whether or not to read in big endian byte ordering.
        /// </summary>
        public bool BigEndian { get; set; }

        /// <summary>
        /// The current position of the reader.
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
        /// How many bytes to read for variable sized values.<para/>
        /// Valid sizes for integers:<br/>
        /// 1,2,4,8<br/>
        /// <br/>
        /// Valid sizes for precise numbers:<br/>
        /// 2,4,8
        /// </summary>
        public int VariableValueSize { get; set; }

        /// <summary>
        /// The length of the underlying memory.
        /// </summary>
        public int Length => _memory.Length;

        /// <summary>
        /// The remaining length starting from the current position.
        /// </summary>
        public int Remaining => Length - Position;

        /// <summary>
        /// The amount of positions the reader is stepped into.
        /// </summary>
        public int StepInCount => _steps.Count;

        /// <summary>
        /// Create a <see cref="BinaryMemoryReader"/> over a region of memory.
        /// </summary>
        /// <param name="memory">A region of memory.</param>
        /// <param name="bigEndian">Whether or not to read in big endian byte ordering.</param>
        public BinaryMemoryReader(Memory<byte> memory, bool bigEndian = false)
        {
            _memory = memory;
            _steps = new Stack<int>();

            BigEndian = bigEndian;
        }

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

        #region Step

        public void StepIn(int position)
        {
            _steps.Push(Position);
            Position = position;
        }

        public void StepOut()
        {
            Position = _steps.Pop();
        }

        #endregion

        #region Memory Read

        public Memory<byte> ReadByteMemory(int size)
        {
            var value = _memory.Slice(Position, size);
            Position += size;
            return value;
        }

        #endregion

        #region Read Helpers

        private T ReadEndian<T>(Func<T, T> reverseEndianness) where T : unmanaged
        {
            var value = Read<T>();
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                return reverseEndianness(value);
            }
            return value;
        }

        private TTo ReadEndianConvert<TFrom, TTo>(Func<TFrom, TFrom> reverseEndianness, Func<TFrom, TTo> convert)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                return convert(reverseEndianness(Read<TFrom>()));
            }
            return Read<TTo>();
        }

        private T[] ReadArrayEndian<T>(Func<T, T> reverseEndianness, int count) where T : unmanaged
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                var values = new T[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = reverseEndianness(Read<T>());
                }
                return values;
            }

            return ReadSpan<T>(count).ToArray();
        }

        private TTo[] ReadArrayEndianConvert<TFrom, TTo>(Func<TFrom, TFrom> reverseEndianness, Func<TFrom, TTo> convert, int count)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                var values = new TTo[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = convert(reverseEndianness(Read<TFrom>()));
                }
                return values;
            }

            return ReadSpan<TTo>(count).ToArray();
        }

        private static T[] ReadArray<T>(Func<T> read, int count) where T : unmanaged
        {
            var values = new T[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = read();
            }
            return values;
        }

        private TTo[] ReadArrayEndianCast<TFrom, TTo>(Func<TFrom, TFrom> reverseEndianness, Func<TFrom, TTo> cast, int count)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            var values = new TTo[count];
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < count; i++)
                {
                    values[i] = cast(reverseEndianness(Read<TFrom>()));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    values[i] = cast(Read<TFrom>());
                }
            }

            return values;
        }

        private TTo[] ReadArrayCheckEndiannessConvertAndCast<TRead, TFrom, TTo>(Func<TRead, TRead> reverseEndianness, Func<TRead, TFrom> convert, Func<TFrom, TTo> cast, int count)
            where TRead : unmanaged
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            var values = new TTo[count];
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < count; i++)
                {
                    values[i] = cast(convert(reverseEndianness(Read<TRead>())));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    values[i] = cast(convert(Read<TRead>()));
                }
            }

            return values;
        }

        private TTo[] ReadArrayAndCast<TFrom, TTo>(Func<TFrom, TTo> cast, int count)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            var values = new TTo[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = cast(Read<TFrom>());
            }
            return values;
        }

        #endregion

        #region Read

        public unsafe T Read<T>() where T : unmanaged
        {
            int size = sizeof(T);
            int endPosition = _position + size;
            if (endPosition > Length)
            {
                throw new InvalidOperationException("Cannot read beyond the specified region of memory.");
            }

            var value = Unsafe.ReadUnaligned<T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(_memory.Span), _position));
            _position = endPosition;
            return value;
        }

        public unsafe Span<T> ReadSpan<T>(int count) where T : unmanaged
        {
            int size = sizeof(T) * count;
            int endPosition = _position + size;
            if (endPosition > Length)
            {
                throw new InvalidOperationException("Cannot read beyond the specified region of memory.");
            }
            var value = MemoryMarshal.Cast<byte, T>(_memory.Span.Slice(_position, size));
            _position = endPosition;
            return value;
        }

        public unsafe T[] ReadArray<T>(int count) where T : unmanaged
            => ReadSpan<T>(count).ToArray();

        #endregion

        #region Get

        public T Get<T>(int position) where T : unmanaged
        {
            var returnPosition = Position;
            Position = position;
            var value = Read<T>();
            Position = returnPosition;
            return value;
        }

        public Span<T> GetSpan<T>(int position, int count) where T : unmanaged
        {
            var returnPosition = Position;
            Position = position;
            var values = ReadSpan<T>(count);
            Position = returnPosition;
            return values;
        }

        public T[] GetArray<T>(int position, int count) where T : unmanaged
        {
            var returnPosition = Position;
            Position = position;
            var values = ReadArray<T>(count);
            Position = returnPosition;
            return values;
        }

        private T Get<T>(Func<T> read, int position)
        {
            var startPosition = Position;
            Position = position;
            var value = read();
            Position = startPosition;
            return value;
        }

        #endregion

        #region Peek

        public unsafe T Peek<T>() where T : unmanaged
        {
            int size = sizeof(T);
            if ((Position + size) > Length)
            {
                throw new InvalidOperationException("Cannot read beyond the specified region of memory.");
            }

            return Unsafe.ReadUnaligned<T>(ref Unsafe.Add(ref MemoryMarshal.GetReference(_memory.Span), Position));
        }

        public unsafe Span<T> PeekSpan<T>(int count) where T : unmanaged
        {
            int size = sizeof(T) * count;
            if ((Position + size) > Length)
            {
                throw new InvalidOperationException("Cannot read beyond the specified region of memory.");
            }
            return MemoryMarshal.Cast<byte, T>(_memory.Span.Slice(Position, size));
        }

        public T[] PeekArray<T>(int count) where T : unmanaged
            => PeekSpan<T>(count).ToArray();

        private T Peek<T>(Func<T> read)
        {
            var startPosition = Position;
            var value = read();
            Position = startPosition;
            return value;
        }

        #endregion

        #region Assert

        private T Assert<T>(T value, string typeName, string valueFormat, T option) where T : IEquatable<T>
        {
            if (value.Equals(option))
            {
                return value;
            }

            string strValue = string.Format(valueFormat, value);
            string strOption = string.Format(valueFormat, option);
            throw new InvalidDataException($"Read {typeName}: {strValue} | Expected: {strOption} | Ending position: 0x{Position:X}");
        }

        private T Assert<T>(T value, string typeName, string valueFormat, ReadOnlySpan<T> options) where T : IEquatable<T>
        {
            foreach (T option in options)
            {
                if (value.Equals(option))
                {
                    return value;
                }
            }

            string strValue = string.Format(valueFormat, value);
            string strOptions = string.Join(", ", options.ToArray().Select(o => string.Format(valueFormat, o)));
            throw new InvalidDataException($"Read {typeName}: {strValue} | Expected: {strOptions} | Ending position: 0x{Position:X}");
        }

        private string AssertString(string value, string encodingName, string option)
        {
            if (value.Equals(option))
            {
                return value;
            }

            throw new InvalidDataException($"Read {encodingName}: {value} | Expected: {option} | Ending position: 0x{Position:X}");
        }

        private string AssertString(string value, string encodingName, ReadOnlySpan<string> options)
        {
            foreach (string option in options)
            {
                if (value.Equals(option))
                {
                    return value;
                }
            }

            string joinedOptions = string.Join(", ", options.ToArray());
            throw new InvalidDataException($"Read {encodingName}: {value} | Expected: {joinedOptions} | Ending position: 0x{Position:X}");
        }

        public void AssertBytePattern(int length, byte pattern)
        {
            byte[] bytes = ReadBytes(length);
            for (int i = 0; i < length; i++)
            {
                if (bytes[i] != pattern)
                {
                    throw new InvalidDataException($"Read {bytes[i]:X2} at position {i} | Expected {length} 0x{pattern:X2}");
                }
            }
        }

        #endregion

        #region SByte

        public sbyte ReadSByte()
            => Read<sbyte>();

        public sbyte[] ReadSBytes(int count)
            => ReadArray<sbyte>(count);

        public sbyte GetSByte(int position)
            => Get<sbyte>(position);

        public sbyte[] GetSBytes(int position, int count)
            => GetArray<sbyte>(position, count);

        public sbyte PeekSByte()
            => Peek<sbyte>();

        public sbyte[] PeekSBytes(int count)
            => PeekArray<sbyte>(count);

        public sbyte AssertSByte(sbyte option)
            => Assert(ReadSByte(), nameof(SByte), "0x{0:X}", option);

        public sbyte AssertSByte(ReadOnlySpan<sbyte> options)
            => Assert(ReadSByte(), nameof(SByte), "0x{0:X}", options);

        #endregion

        #region Byte

        public byte ReadByte()
            => Read<byte>();

        public byte[] ReadBytes(int count)
            => ReadArray<byte>(count);

        public byte GetByte(int position)
            => Get<byte>(position);

        public byte[] GetBytes(int position, int count)
            => GetArray<byte>(position, count);

        public byte PeekByte()
            => Peek<byte>();

        public byte[] PeekBytes(int count)
            => PeekArray<byte>(count);

        public byte AssertByte(byte option)
            => Assert(ReadByte(), nameof(Byte), "0x{0:X}", option);

        public byte AssertByte(ReadOnlySpan<byte> options)
            => Assert(ReadByte(), nameof(Byte), "0x{0:X}", options);

        #endregion

        #region Int16

        public short ReadInt16()
            => ReadEndian<short>(BinaryPrimitives.ReverseEndianness);

        public short[] ReadInt16s(int count)
            => ReadArrayEndian<short>(BinaryPrimitives.ReverseEndianness, count);

        public short GetInt16(int position)
            => Get(ReadInt16, position);

        public short[] GetInt16s(int position, int count)
            => Get(() => ReadInt16s(count), position);

        public short PeekInt16()
            => Peek(ReadInt16);

        public short[] PeekInt16s(int count)
            => Peek(() => ReadInt16s(count));

        public short AssertInt16(short option)
            => Assert(ReadInt16(), nameof(Int16), "0x{0:X}", option);

        public short AssertInt16(ReadOnlySpan<short> options)
            => Assert(ReadInt16(), nameof(Int16), "0x{0:X}", options);

        #endregion

        #region UInt16

        public ushort ReadUInt16()
            => ReadEndian<ushort>(BinaryPrimitives.ReverseEndianness);

        public ushort[] ReadUInt16s(int count)
            => ReadArrayEndian<ushort>(BinaryPrimitives.ReverseEndianness, count);

        public ushort GetUInt16(int position)
            => Get(ReadUInt16, position);

        public ushort[] GetUInt16s(int position, int count)
            => Get(() => ReadUInt16s(count), position);

        public ushort PeekUInt16()
            => Peek(ReadUInt16);

        public ushort[] PeekUInt16s(int count)
            => Peek(() => ReadUInt16s(count));

        public ushort AssertUInt16(ushort option)
            => Assert(ReadUInt16(), nameof(UInt16), "0x{0:X}", option);

        public ushort AssertUInt16(ReadOnlySpan<ushort> options)
            => Assert(ReadUInt16(), nameof(UInt16), "0x{0:X}", options);

        #endregion

        #region Int32

        public int ReadInt32()
            => ReadEndian<int>(BinaryPrimitives.ReverseEndianness);

        public int[] ReadInt32s(int count)
            => ReadArrayEndian<int>(BinaryPrimitives.ReverseEndianness, count);

        public int GetInt32(int position)
            => Get(ReadInt32, position);

        public int[] GetInt32s(int position, int count)
            => Get(() => ReadInt32s(count), position);

        public int PeekInt32()
            => Peek(ReadInt32);

        public int[] PeekInt32s(int count)
            => Peek(() => ReadInt32s(count));

        public int AssertInt32(int option)
            => Assert(ReadInt32(), nameof(Int32), "0x{0:X}", option);

        public int AssertInt32(ReadOnlySpan<int> options)
            => Assert(ReadInt32(), nameof(Int32), "0x{0:X}", options);

        #endregion

        #region UInt32

        public uint ReadUInt32()
            => ReadEndian<uint>(BinaryPrimitives.ReverseEndianness);

        public uint[] ReadUInt32s(int count)
            => ReadArrayEndian<uint>(BinaryPrimitives.ReverseEndianness, count);

        public uint GetUInt32(int position)
            => Get(ReadUInt32, position);

        public uint[] GetUInt32s(int position, int count)
            => Get(() => ReadUInt32s(count), position);

        public uint PeekUInt32()
            => Peek(ReadUInt32);

        public uint[] PeekUInt32s(int count)
            => Peek(() => ReadUInt32s(count));

        public uint AssertUInt32(uint option)
            => Assert(ReadUInt32(), nameof(UInt32), "0x{0:X}", option);

        public uint AssertUInt32(ReadOnlySpan<uint> options)
            => Assert(ReadUInt32(), nameof(UInt32), "0x{0:X}", options);

        #endregion

        #region Int64

        public long ReadInt64()
            => ReadEndian<long>(BinaryPrimitives.ReverseEndianness);

        public long[] ReadInt64s(int count)
            => ReadArrayEndian<long>(BinaryPrimitives.ReverseEndianness, count);

        public long GetInt64(int position)
            => Get(ReadInt64, position);

        public long[] GetInt64s(int position, int count)
            => Get(() => ReadInt64s(count), position);

        public long PeekInt64()
            => Peek(ReadInt64);

        public long[] PeekInt64s(int count)
            => Peek(() => ReadInt64s(count));

        public long AssertInt64(long option)
            => Assert(ReadInt64(), nameof(Int64), "0x{0:X}", option);

        public long AssertInt64(ReadOnlySpan<long> options)
            => Assert(ReadInt64(), nameof(Int64), "0x{0:X}", options);

        #endregion

        #region UInt64

        public ulong ReadUInt64()
            => ReadEndian<ulong>(BinaryPrimitives.ReverseEndianness);

        public ulong[] ReadUInt64s(int count)
            => ReadArrayEndian<ulong>(BinaryPrimitives.ReverseEndianness, count);

        public ulong GetUInt64(int position)
            => Get(ReadUInt64, position);

        public ulong[] GetUInt64s(int position, int count)
            => Get(() => ReadUInt64s(count), position);

        public ulong PeekUInt64()
            => Peek(ReadUInt64);

        public ulong[] PeekUInt64s(int count)
            => Peek(() => ReadUInt64s(count));

        public ulong AssertUInt64(ulong option)
            => Assert(ReadUInt64(), nameof(UInt64), "0x{0:X}", option);

        public ulong AssertUInt64(ReadOnlySpan<ulong> options)
            => Assert(ReadUInt64(), nameof(UInt64), "0x{0:X}", options);

        #endregion

        #region Int128

        public Int128 ReadInt128()
            => ReadEndian<Int128>(BinaryPrimitives.ReverseEndianness);

        public Int128[] ReadInt128s(int count)
            => ReadArrayEndian<Int128>(BinaryPrimitives.ReverseEndianness, count);

        public Int128 GetInt128(int position)
            => Get(ReadInt128, position);

        public Int128[] GetInt128s(int position, int count)
            => Get(() => ReadInt128s(count), position);

        public Int128 PeekInt128()
            => Peek(ReadInt128);

        public Int128[] PeekInt128s(int count)
            => Peek(() => ReadInt128s(count));

        public Int128 AssertInt128(Int128 option)
            => Assert(ReadInt128(), nameof(Int128), "0x{0:X}", option);

        public Int128 AssertInt128(ReadOnlySpan<Int128> options)
            => Assert(ReadInt128(), nameof(Int128), "0x{0:X}", options);

        #endregion

        #region UInt128

        public UInt128 ReadUInt128()
            => ReadEndian<UInt128>(BinaryPrimitives.ReverseEndianness);

        public UInt128[] ReadUInt128s(int count)
            => ReadArrayEndian<UInt128>(BinaryPrimitives.ReverseEndianness, count);

        public UInt128 GetUInt128(int position)
            => Get(ReadUInt128, position);

        public UInt128[] GetUInt128s(int position, int count)
            => Get(() => ReadUInt128s(count), position);

        public UInt128 PeekUInt128()
            => Peek(ReadUInt128);

        public UInt128[] PeekUInt128s(int count)
            => Peek(() => ReadUInt128s(count));

        public UInt128 AssertUInt128(UInt128 option)
            => Assert(ReadUInt128(), nameof(UInt128), "0x{0:X}", option);

        public UInt128 AssertUInt128(ReadOnlySpan<UInt128> options)
            => Assert(ReadUInt128(), nameof(UInt128), "0x{0:X}", options);

        #endregion

        #region Half

        public Half ReadHalf()
            => ReadEndianConvert<ushort, Half>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt16BitsToHalf);

        public Half[] ReadHalfs(int count)
            => ReadArrayEndianConvert<ushort, Half>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt16BitsToHalf, count);

        public Half GetHalf(int position)
            => Get(ReadHalf, position);

        public Half[] GetHalfs(int position, int count)
            => Get(() => ReadHalfs(count), position);

        public Half PeekHalf()
            => Peek(ReadHalf);

        public Half[] PeekHalfs(int count)
            => Peek(() => ReadHalfs(count));

        public Half AssertHalf(Half option)
            => Assert(ReadHalf(), nameof(Half), "{0}", option);

        public Half AssertHalf(ReadOnlySpan<Half> options)
            => Assert(ReadHalf(), nameof(Half), "{0}", options);

        #endregion

        #region Single

        public float ReadSingle()
            => ReadEndianConvert<uint, float>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt32BitsToSingle);

        public float[] ReadSingles(int count)
            => ReadArrayEndianConvert<uint, float>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt32BitsToSingle, count);

        public float GetSingle(int position)
            => Get(ReadSingle, position);

        public float[] GetSingles(int position, int count)
            => Get(() => ReadSingles(count), position);

        public float PeekSingle()
            => Peek(ReadSingle);

        public float[] PeekSingles(int count)
            => Peek(() => ReadSingles(count));

        public float AssertSingle(float option)
            => Assert(ReadSingle(), nameof(Single), "{0}", option);

        public float AssertSingle(ReadOnlySpan<float> options)
            => Assert(ReadSingle(), nameof(Single), "{0}", options);

        #endregion

        #region Double

        public double ReadDouble()
            => ReadEndianConvert<ulong, double>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt64BitsToDouble);

        public double[] ReadDoubles(int count)
            => ReadArrayEndianConvert<ulong, double>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt64BitsToDouble, count);

        public double GetDouble(int position)
            => Get(ReadDouble, position);

        public double[] GetDoubles(int position, int count)
            => Get(() => ReadDoubles(count), position);

        public double PeekDouble()
            => Peek(ReadDouble);

        public double[] PeekDoubles(int count)
            => Peek(() => ReadDoubles(count));

        public double AssertDouble(double option)
            => Assert(ReadDouble(), nameof(Double), "{0}", option);

        public double AssertDouble(ReadOnlySpan<double> options)
            => Assert(ReadDouble(), nameof(Double), "{0}", options);

        #endregion

        #region Boolean

#if UNSTRICT_READ_BOOLEAN
        public bool ReadBoolean()
            => ReadByte() != 0;
#else
        public bool ReadBoolean()
        {
            byte value = ReadByte();
            if (value == 0)
            {
                return false;
            }
            else if (value == 1)
            {
                return true;
            }

            throw new InvalidDataException($"{nameof(ReadBoolean)} read invalid {nameof(Boolean)} value: {value}");
        }
#endif

        public bool[] ReadBooleans(int count)
            => ReadArray(ReadBoolean, count);

        public bool GetBoolean(int position)
            => Get(ReadBoolean, position);

        public bool[] GetBooleans(int position, int count)
            => Get(() => ReadBooleans(count), position);

        public bool PeekBoolean()
            => Peek(ReadBoolean);

        public bool[] PeekBooleans(int count)
            => Peek(() => ReadBooleans(count));

        public bool AssertBoolean(bool option)
            => Assert(ReadBoolean(), nameof(Boolean), "{0}", option);

        #endregion

        #region String

        public string ReadUTF8()
            => Encoding.UTF8.GetString(Read8BitTerminatedStringBytes());

        public string ReadASCII()
            => Encoding.ASCII.GetString(Read8BitTerminatedStringBytes());

        public string ReadShiftJIS()
            => EncodingHelper.ShiftJIS.GetString(Read8BitTerminatedStringBytes());

        public string ReadEucJP()
            => EncodingHelper.EucJP.GetString(Read8BitTerminatedStringBytes());

        public string ReadEucCN()
            => EncodingHelper.EucCN.GetString(Read8BitTerminatedStringBytes());

        public string ReadEucKR()
            => EncodingHelper.EucKR.GetString(Read8BitTerminatedStringBytes());

        public string ReadUTF16()
            => BigEndian ? EncodingHelper.UTF16BE.GetString(Read16BitTerminatedStringBytes()) : EncodingHelper.UTF16LE.GetString(Read16BitTerminatedStringBytes());

        public string ReadUTF16LittleEndian()
            => EncodingHelper.UTF16LE.GetString(Read16BitTerminatedStringBytes());

        public string ReadUTF16BigEndian()
            => EncodingHelper.UTF16BE.GetString(Read16BitTerminatedStringBytes());

        public string ReadUTF32()
            => BigEndian ? EncodingHelper.UTF32BE.GetString(Read32BitTerminatedStringBytes()) : EncodingHelper.UTF32LE.GetString(Read32BitTerminatedStringBytes());

        public string ReadUTF32LittleEndian()
            => EncodingHelper.UTF32LE.GetString(Read32BitTerminatedStringBytes());

        public string ReadUTF32BigEndian()
            => EncodingHelper.UTF32BE.GetString(Read32BitTerminatedStringBytes());

        public string ReadUTF8(int length)
            => Encoding.UTF8.GetString(ReadBytes(length));

        public string ReadASCII(int length)
            => Encoding.ASCII.GetString(ReadBytes(length));

        public string ReadShiftJIS(int length)
            => EncodingHelper.ShiftJIS.GetString(ReadBytes(length));

        public string ReadEucJP(int length)
            => EncodingHelper.EucJP.GetString(ReadBytes(length));

        public string ReadEucCN(int length)
            => EncodingHelper.EucCN.GetString(ReadBytes(length));

        public string ReadEucKR(int length)
            => EncodingHelper.EucKR.GetString(ReadBytes(length));

        public string ReadUTF16(int length)
            => BigEndian ? EncodingHelper.UTF16BE.GetString(ReadBytes(length * 2)) : EncodingHelper.UTF16LE.GetString(ReadBytes(length * 2));

        public string ReadUTF16LittleEndian(int length)
            => EncodingHelper.UTF16LE.GetString(ReadBytes(length * 2));

        public string ReadUTF16BigEndian(int length)
            => EncodingHelper.UTF16BE.GetString(ReadBytes(length * 2));

        public string ReadUTF32(int length)
            => BigEndian ? EncodingHelper.UTF32BE.GetString(ReadBytes(length * 4)) : EncodingHelper.UTF32LE.GetString(ReadBytes(length * 4));

        public string ReadUTF32LittleEndian(int length)
            => EncodingHelper.UTF32LE.GetString(ReadBytes(length * 4));

        public string ReadUTF32BigEndian(int length)
            => EncodingHelper.UTF32BE.GetString(ReadBytes(length * 4));

        public string GetUTF8(int position)
            => Get(ReadUTF8, position);

        public string GetASCII(int position)
            => Get(ReadASCII, position);

        public string GetShiftJIS(int position)
            => Get(ReadShiftJIS, position);

        public string GetEucJP(int position)
            => Get(ReadEucJP, position);

        public string GetEucCN(int position)
            => Get(ReadEucCN, position);

        public string GetEucKR(int position)
            => Get(ReadEucKR, position);

        public string GetUTF16(int position)
            => Get(ReadUTF16, position);

        public string GetUTF16LittleEndian(int position)
            => Get(ReadUTF16LittleEndian, position);

        public string GetUTF16BigEndian(int position)
            => Get(ReadUTF16BigEndian, position);

        public string GetUTF32(int position)
            => Get(ReadUTF32, position);

        public string GetUTF32LittleEndian(int position)
            => Get(ReadUTF32LittleEndian, position);

        public string GetUTF32BigEndian(int position)
            => Get(ReadUTF32BigEndian, position);

        public string GetUTF8(int position, int length)
            => Get(() => ReadUTF8(length), position);

        public string GetASCII(int position, int length)
            => Get(() => ReadASCII(length), position);

        public string GetShiftJIS(int position, int length)
            => Get(() => ReadShiftJIS(length), position);

        public string GetEucJP(int position, int length)
            => Get(() => ReadEucJP(length), position);

        public string GetEucCN(int position, int length)
            => Get(() => ReadEucCN(length), position);

        public string GetEucKR(int position, int length)
            => Get(() => ReadEucKR(length), position);

        public string GetUTF16(int position, int length)
            => Get(() => ReadUTF16(length), position);

        public string GetUTF16LittleEndian(int position, int length)
            => Get(() => ReadUTF16LittleEndian(length), position);

        public string GetUTF16BigEndian(int position, int length)
            => Get(() => ReadUTF16BigEndian(length), position);

        public string GetUTF32(int position, int length)
            => Get(() => ReadUTF32(length), position);

        public string GetUTF32LittleEndian(int position, int length)
            => Get(() => ReadUTF32LittleEndian(length), position);

        public string GetUTF32BigEndian(int position, int length)
            => Get(() => ReadUTF32BigEndian(length), position);

        public string PeekUTF8()
            => Peek(ReadUTF8);

        public string PeekASCII()
            => Peek(ReadASCII);

        public string PeekShiftJIS()
            => Peek(ReadShiftJIS);

        public string PeekEucJP()
            => Peek(ReadEucJP);

        public string PeekEucCN()
            => Peek(ReadEucCN);

        public string PeekEucKR()
            => Peek(ReadEucKR);

        public string PeekUTF16()
            => Peek(ReadUTF16);

        public string PeekUTF16LittleEndian()
            => Peek(ReadUTF16LittleEndian);

        public string PeekUTF16BigEndian()
            => Peek(ReadUTF16BigEndian);

        public string PeekUTF32()
            => Peek(ReadUTF32);

        public string PeekUTF32LittleEndian()
            => Peek(ReadUTF32LittleEndian);

        public string PeekUTF32BigEndian()
            => Peek(ReadUTF32BigEndian);

        public string PeekUTF8(int length)
            => Peek(() => ReadUTF8(length));

        public string PeekASCII(int length)
            => Peek(() => ReadASCII(length));

        public string PeekShiftJIS(int length)
            => Peek(() => ReadShiftJIS(length));

        public string PeekEucJP(int length)
            => Peek(() => ReadEucJP(length));

        public string PeekEucCN(int length)
            => Peek(() => ReadEucCN(length));

        public string PeekEucKR(int length)
            => Peek(() => ReadEucKR(length));

        public string PeekUTF16(int length)
            => Peek(() => ReadUTF16(length));

        public string PeekUTF16LittleEndian(int length)
            => Peek(() => ReadUTF16LittleEndian(length));

        public string PeekUTF16BigEndian(int length)
            => Peek(() => ReadUTF16BigEndian(length));

        public string PeekUTF32(int length)
            => Peek(() => ReadUTF32(length));

        public string PeekUTF32LittleEndian(int length)
            => Peek(() => ReadUTF32LittleEndian(length));

        public string PeekUTF32BigEndian(int length)
            => Peek(() => ReadUTF32BigEndian(length));

        public string AssertUTF8(string option)
            => AssertString(ReadUTF8(option.Length), "UTF8", option);

        public string AssertUTF8(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF8(length), "UTF8", options);

        public string AssertASCII(string option)
            => AssertString(ReadASCII(option.Length), "ASCII", option);

        public string AssertASCII(int length, ReadOnlySpan<string> options)
            => AssertString(ReadASCII(length), "ASCII", options);

        public string AssertShiftJIS(string option)
            => AssertString(ReadShiftJIS(option.Length), "ShiftJIS", option);

        public string AssertShiftJIS(int length, ReadOnlySpan<string> options)
            => AssertString(ReadShiftJIS(length), "ShiftJIS", options);

        public string AssertEucJP(string option)
            => AssertString(ReadEucJP(option.Length), "EucJP", option);

        public string AssertEucJP(int length, ReadOnlySpan<string> options)
            => AssertString(ReadEucJP(length), "EucJP", options);

        public string AssertEucCN(string option)
            => AssertString(ReadEucCN(option.Length), "EucCN", option);

        public string AssertEucCN(int length, ReadOnlySpan<string> options)
            => AssertString(ReadEucCN(length), "EucCN", options);

        public string AssertEucKR(string option)
            => AssertString(ReadEucKR(option.Length), "EucKR", option);

        public string AssertEucKR(int length, ReadOnlySpan<string> options)
            => AssertString(ReadEucKR(length), "EucKR", options);

        public string AssertUTF16(string option)
            => AssertString(ReadUTF16(option.Length), "UTF16", option);

        public string AssertUTF16(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF16(length), "UTF16", options);

        public string AssertUTF16LittleEndian(string option)
            => AssertString(ReadUTF16LittleEndian(option.Length), "UTF16LittleEndian", option);

        public string AssertUTF16LittleEndian(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF16LittleEndian(length), "UTF16LittleEndian", options);

        public string AssertUTF16BigEndian(string option)
            => AssertString(ReadUTF16BigEndian(option.Length), "UTF16BigEndian", option);

        public string AssertUTF16BigEndian(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF16BigEndian(length), "UTF16BigEndian", options);

        public string AssertUTF32(string option)
            => AssertString(ReadUTF32(option.Length), "UTF32", option);

        public string AssertUTF32(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF32(length), "UTF32", options);

        public string AssertUTF32LittleEndian(string option)
            => AssertString(ReadUTF32LittleEndian(option.Length), "UTF32LittleEndian", option);

        public string AssertUTF32LittleEndian(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF32LittleEndian(length), "UTF32LittleEndian", options);

        public string AssertUTF32BigEndian(string option)
            => AssertString(ReadUTF32BigEndian(option.Length), "UTF32BigEndian", option);

        public string AssertUTF32BigEndian(int length, ReadOnlySpan<string> options)
            => AssertString(ReadUTF32BigEndian(length), "UTF32BigEndian", options);

        #region String Termination

        protected byte[] Read8BitTerminatedStringBytes()
        {
            var bytes = new List<byte>();
            byte b = ReadByte();
            while (b != 0)
            {
                bytes.Add(b);
                b = ReadByte();
            }
            return [.. bytes];
        }

        protected byte[] Read16BitTerminatedStringBytes()
        {
            var bytes = new List<byte>();
            byte a = ReadByte();
            byte b = ReadByte();
            while ((a | b) != 0)
            {
                bytes.Add(a);
                bytes.Add(b);
                a = ReadByte();
                b = ReadByte();
            }
            return [.. bytes];
        }

        protected byte[] Read32BitTerminatedStringBytes()
        {
            var bytes = new List<byte>();
            byte a = ReadByte();
            byte b = ReadByte();
            byte c = ReadByte();
            byte d = ReadByte();
            while ((a | b | c | d) != 0)
            {
                bytes.Add(a);
                bytes.Add(b);
                bytes.Add(c);
                bytes.Add(d);
                a = ReadByte();
                b = ReadByte();
                c = ReadByte();
                d = ReadByte();
            }
            return [.. bytes];
        }

        protected byte[] ReadTerminatedStringBytes(int bytesPerChar)
        {
            var bytes = new List<byte>();
            byte[] readBytes = ReadBytes(bytesPerChar);

            bool IsNull()
            {
                foreach (byte b in readBytes)
                {
                    if (b != 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            while (!IsNull())
            {
                bytes.AddRange(readBytes);
                readBytes = ReadBytes(bytesPerChar);
            }

            return bytes.ToArray();
        }

        #endregion

        #endregion

        #region Vector2

        public Vector2 ReadVector2()
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                float x = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float y = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                return new Vector2(x, y);
            }

            return Read<Vector2>();
        }

        public Vector2[] ReadVector2s(int count)
        {
            // Prevent checking endianness for every component that needs to be read
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                // Read all components in reversed byte order
                int componentCount = count * 2;
                var values = new float[componentCount];
                for (int i = 0; i < componentCount; i++)
                {
                    values[i] = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                }

                // Reinterpret components as values
                return MemoryMarshal.Cast<float, Vector2>(new ReadOnlySpan<float>(values)).ToArray();
            }

            return ReadArray<Vector2>(count);
        }

        public Vector2 GetVector2(int position)
            => Get(ReadVector2, position);

        public Vector2[] GetVector2s(int position, int count)
            => Get(() => ReadVector2s(count), position);

        public Vector2 PeekVector2()
            => Peek(ReadVector2);

        public Vector2[] PeekVector2s(int count)
            => Peek(() => ReadVector2s(count));

        #endregion

        #region Vector3

        public Vector3 ReadVector3()
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                float x = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float y = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float z = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                return new Vector3(x, y, z);
            }

            return Read<Vector3>();
        }

        public Vector3[] ReadVector3s(int count)
        {
            // Prevent checking endianness for every component that needs to be read
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                // Read all components in reversed byte order
                int componentCount = count * 3;
                var values = new float[componentCount];
                for (int i = 0; i < componentCount; i++)
                {
                    values[i] = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                }

                // Reinterpret components as values
                return MemoryMarshal.Cast<float, Vector3>(new ReadOnlySpan<float>(values)).ToArray();
            }

            return ReadArray<Vector3>(count);
        }

        public Vector3 GetVector3(int position)
            => Get(ReadVector3, position);

        public Vector3[] GetVector3s(int position, int count)
            => Get(() => ReadVector3s(count), position);

        public Vector3 PeekVector3()
            => Peek(ReadVector3);

        public Vector3[] PeekVector3s(int count)
            => Peek(() => ReadVector3s(count));

        #endregion

        #region Vector4

        public Vector4 ReadVector4()
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                float x = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float y = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float z = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float w = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                return new Vector4(x, y, z, w);
            }

            return Read<Vector4>();
        }

        public Vector4[] ReadVector4s(int count)
        {
            // Prevent checking endianness for every component that needs to be read
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                // Read all components in reversed byte order
                int componentCount = count * 4;
                var values = new float[componentCount];
                for (int i = 0; i < componentCount; i++)
                {
                    values[i] = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                }

                // Reinterpret components as values
                return MemoryMarshal.Cast<float, Vector4>(new ReadOnlySpan<float>(values)).ToArray();
            }

            return ReadArray<Vector4>(count);
        }

        public Vector4 GetVector4(int position)
            => Get(ReadVector4, position);

        public Vector4[] GetVector4s(int position, int count)
            => Get(() => ReadVector4s(count), position);

        public Vector4 PeekVector4()
            => Peek(ReadVector4);

        public Vector4[] PeekVector4s(int count)
            => Peek(() => ReadVector4s(count));

        #endregion

        #region Quaternion

        public Quaternion ReadQuaternion()
        {
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                float x = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float y = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float z = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                float w = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                return new Quaternion(x, y, z, w);
            }

            return Read<Quaternion>();
        }

        public Quaternion[] ReadQuaternions(int count)
        {
            // Prevent checking endianness for every component that needs to be read
            if (BigEndian != !BitConverter.IsLittleEndian)
            {
                // Read all components in reversed byte order
                int componentCount = count * 4;
                var values = new float[componentCount];
                for (int i = 0; i < componentCount; i++)
                {
                    values[i] = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReverseEndianness(Read<uint>()));
                }

                // Reinterpret components as values
                return MemoryMarshal.Cast<float, Quaternion>(new ReadOnlySpan<float>(values)).ToArray();
            }

            return ReadArray<Quaternion>(count);
        }

        public Quaternion GetQuaternion(int position)
            => Get(ReadQuaternion, position);

        public Quaternion[] GetQuaternions(int position, int count)
            => Get(() => ReadQuaternions(count), position);

        public Quaternion PeekQuaternion()
            => Peek(ReadQuaternion);

        public Quaternion[] PeekQuaternions(int count)
            => Peek(() => ReadQuaternions(count));

        #endregion

        #region Color3

        public byte[] ReadColor3Raw()
            => ReadBytes(3);

        public Color ReadColorRGB()
        {
            var bytes = ReadSpan<byte>(3);
            return Color.FromArgb(255, bytes[0], bytes[1], bytes[2]);
        }

        public Color ReadColorBGR()
        {
            var bytes = ReadSpan<byte>(3);
            return Color.FromArgb(255, bytes[2], bytes[1], bytes[0]);
        }

        public byte[] GetColor3Raw(int position)
            => Get(ReadColor3Raw, position);

        public Color GetColorRGB(int position)
            => Get(ReadColorRGB, position);

        public Color GetColorBGR(int position)
            => Get(ReadColorBGR, position);

        public byte[] PeekColor3Raw()
            => Peek(ReadColor3Raw);

        public Color PeekColorRGB()
            => Peek(ReadColorRGB);

        public Color PeekColorBGR()
            => Peek(ReadColorBGR);

        #endregion

        #region Color4

        public byte[] ReadColor4Raw()
            => ReadBytes(4);

        public Color ReadColorRGBA()
        {
            var bytes = ReadSpan<byte>(4);
            return Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2]);
        }

        public Color ReadColorBGRA()
        {
            var bytes = ReadSpan<byte>(4);
            return Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
        }

        public Color ReadColorARGB()
        {
            var bytes = ReadSpan<byte>(4);
            return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
        }

        public Color ReadColorABGR()
        {
            var bytes = ReadSpan<byte>(4);
            return Color.FromArgb(bytes[0], bytes[3], bytes[2], bytes[1]);
        }

        public byte[] GetColor4Raw(int position)
            => Get(ReadColor4Raw, position);

        public Color GetColorRGBA(int position)
            => Get(ReadColorRGBA, position);

        public Color GetColorBGRA(int position)
            => Get(ReadColorBGRA, position);

        public Color GetColorARGB(int position)
            => Get(ReadColorARGB, position);

        public Color GetColorABGR(int position)
            => Get(ReadColorABGR, position);

        public byte[] PeekColor4Raw()
            => Peek(ReadColor4Raw);

        public Color PeekColorRGBA()
            => Peek(ReadColorRGBA);

        public Color PeekColorBGRA()
            => Peek(ReadColorBGRA);

        public Color PeekColorARGB()
            => Peek(ReadColorARGB);

        public Color PeekColorABGR()
            => Peek(ReadColorABGR);

        #endregion

        #region Enum

        private static TEnum ReadEnum<TEnum, TValue>(Func<TValue> read, string valueFormat)
            where TEnum : Enum
            where TValue : notnull
        {
            TValue value = read();
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new InvalidDataException($"Read value not present in enum: {string.Format(valueFormat, value)}");
            }
            return (TEnum)(object)value;
        }

        public TEnum ReadEnum<TEnum>() where TEnum : Enum
        {
            Type type = Enum.GetUnderlyingType(typeof(TEnum));
            if (type == typeof(byte))
            {
                return ReadEnum<TEnum, byte>(ReadByte, "0x{0:X}");
            }

            if (type == typeof(sbyte))
            {
                return ReadEnum<TEnum, sbyte>(ReadSByte, "0x{0:X}");
            }

            if (type == typeof(short))
            {
                return ReadEnum<TEnum, short>(ReadInt16, "0x{0:X}");
            }

            if (type == typeof(ushort))
            {
                return ReadEnum<TEnum, ushort>(ReadUInt16, "0x{0:X}");
            }

            if (type == typeof(int))
            {
                return ReadEnum<TEnum, int>(ReadInt32, "0x{0:X}");
            }

            if (type == typeof(uint))
            {
                return ReadEnum<TEnum, uint>(ReadUInt32, "0x{0:X}");
            }

            if (type == typeof(long))
            {
                return ReadEnum<TEnum, long>(ReadInt64, "0x{0:X}");
            }

            if (type == typeof(ulong))
            {
                return ReadEnum<TEnum, ulong>(ReadUInt64, "0x{0:X}");
            }

            throw new InvalidDataException($"Enum {typeof(TEnum).Name} has an unknown underlying value type: {type.Name}");
        }

        public TEnum ReadEnum8<TEnum>() where TEnum : Enum
        {
            Type type = Enum.GetUnderlyingType(typeof(TEnum));
            if (type == typeof(byte))
            {
                return ReadEnum<TEnum, byte>(ReadByte, "0x{0:X}");
            }

            if (type == typeof(sbyte))
            {
                return ReadEnum<TEnum, sbyte>(ReadSByte, "0x{0:X}");
            }

            throw new InvalidDataException($"Enum {typeof(TEnum).Name} has an invalid underlying value type: {type.Name}");
        }

        public TEnum ReadEnum16<TEnum>() where TEnum : Enum
        {
            Type type = Enum.GetUnderlyingType(typeof(TEnum));
            if (type == typeof(short))
            {
                return ReadEnum<TEnum, short>(ReadInt16, "0x{0:X}");
            }

            if (type == typeof(ushort))
            {
                return ReadEnum<TEnum, ushort>(ReadUInt16, "0x{0:X}");
            }

            throw new InvalidDataException($"Enum {typeof(TEnum).Name} has an invalid underlying value type: {type.Name}");
        }

        public TEnum ReadEnum32<TEnum>() where TEnum : Enum
        {
            Type type = Enum.GetUnderlyingType(typeof(TEnum));
            if (type == typeof(int))
            {
                return ReadEnum<TEnum, int>(ReadInt32, "0x{0:X}");
            }

            if (type == typeof(uint))
            {
                return ReadEnum<TEnum, uint>(ReadUInt32, "0x{0:X}");
            }

            throw new InvalidDataException($"Enum {typeof(TEnum).Name} has an invalid underlying value type: {type.Name}");
        }

        public TEnum ReadEnum64<TEnum>() where TEnum : Enum
        {
            Type type = Enum.GetUnderlyingType(typeof(TEnum));
            if (type == typeof(long))
            {
                return ReadEnum<TEnum, long>(ReadInt64, "0x{0:X}");
            }

            if (type == typeof(ulong))
            {
                return ReadEnum<TEnum, ulong>(ReadUInt64, "0x{0:X}");
            }

            throw new InvalidDataException($"Enum {typeof(TEnum).Name} has an invalid underlying value type: {type.Name}");
        }

        #endregion

        #region VariableValue

        public long ReadVarValSigned()
        {
            return VariableValueSize switch
            {
                1 => ReadSByte(),
                2 => ReadInt16(),
                4 => ReadInt32(),
                8 => ReadInt64(),
                _ => throw new NotSupportedException($"{nameof(VariableValueSize)} {VariableValueSize} is not supported for {ReadVarValSigned}"),
            };
        }

        public long[] ReadVarValsSigned(int count)
        {
            return VariableValueSize switch
            {
                1 => ReadArrayAndCast<sbyte, long>(Convert.ToInt64, count),
                2 => ReadArrayEndianCast<short, long>(BinaryPrimitives.ReverseEndianness, Convert.ToInt64, count),
                4 => ReadArrayEndianCast<int, long>(BinaryPrimitives.ReverseEndianness, Convert.ToInt64, count),
                8 => ReadInt64s(count),
                _ => throw new NotSupportedException($"{nameof(VariableValueSize)} {VariableValueSize} is not supported for {ReadVarValsSigned}"),
            };
        }

        public ulong ReadVarValUnsigned()
        {
            return VariableValueSize switch
            {
                1 => ReadByte(),
                2 => ReadUInt16(),
                4 => ReadUInt32(),
                8 => ReadUInt64(),
                _ => throw new NotSupportedException($"{nameof(VariableValueSize)} {VariableValueSize} is not supported for {ReadVarValUnsigned}"),
            };
        }

        public ulong[] ReadVarValsUnsigned(int count)
        {
            return VariableValueSize switch
            {
                1 => ReadArrayAndCast<byte, ulong>(Convert.ToUInt64, count),
                2 => ReadArrayEndianCast<ushort, ulong>(BinaryPrimitives.ReverseEndianness, Convert.ToUInt64, count),
                4 => ReadArrayEndianCast<uint, ulong>(BinaryPrimitives.ReverseEndianness, Convert.ToUInt64, count),
                8 => ReadUInt64s(count),
                _ => throw new NotSupportedException($"{nameof(VariableValueSize)} {VariableValueSize} is not supported for {ReadVarValsUnsigned}"),
            };
        }

        public double ReadVarValPrecise()
        {
            return VariableValueSize switch
            {
                2 => (double)ReadHalf(),
                4 => ReadSingle(),
                8 => ReadDouble(),
                _ => throw new NotSupportedException($"{nameof(VariableValueSize)} {VariableValueSize} is not supported for {ReadVarValPrecise}"),
            };
        }

        public double[] ReadVarValsPrecise(int count)
        {
            return VariableValueSize switch
            {
                2 => ReadArrayCheckEndiannessConvertAndCast<ushort, Half, double>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt16BitsToHalf, ConvertHelper.ToDouble, count),
                4 => ReadArrayCheckEndiannessConvertAndCast<uint, float, double>(BinaryPrimitives.ReverseEndianness, BitConverter.UInt32BitsToSingle, Convert.ToDouble, count),
                8 => ReadDoubles(count),
                _ => throw new NotSupportedException($"{nameof(VariableValueSize)} {VariableValueSize} is not supported for {ReadVarValsPrecise}"),
            };
        }

        public long GetVarValSigned(int position)
            => Get(ReadVarValSigned, position);

        public long[] GetVarValsSigned(int position, int count)
            => Get(() => ReadVarValsSigned(count), position);

        public ulong GetVarValUnsigned(int position)
            => Get(ReadVarValUnsigned, position);

        public ulong[] GetVarValsUnsigned(int position, int count)
            => Get(() => ReadVarValsUnsigned(count), position);

        public double GetVarValPrecise(int position)
            => Get(ReadVarValPrecise, position);

        public double[] GetVarValPrecise(int position, int count)
            => Get(() => ReadVarValsPrecise(count), position);

        public long PeekVarValSigned()
            => Peek(ReadVarValSigned);

        public long[] PeekVarValsSigned(int count)
            => Peek(() => ReadVarValsSigned(count));

        public ulong PeekVarValUnsigned()
            => Peek(ReadVarValUnsigned);

        public ulong[] PeekVarValsUnsigned(int count)
            => Peek(() => ReadVarValsUnsigned(count));

        public double PeekVarValPrecise()
            => Peek(ReadVarValPrecise);

        public double[] PeekVarValPrecise(int count)
            => Peek(() => ReadVarValsPrecise(count));

        public long AssertVarValSigned(long option)
            => Assert(ReadVarValSigned(), $"VarValS{VariableValueSize}", "0x{0:X}", option);

        public long AssertVarValSigned(ReadOnlySpan<long> options)
            => Assert(ReadVarValSigned(), $"VarValS{VariableValueSize}", "0x{0:X}", options);

        public ulong AssertVarValUnsigned(ulong option)
            => Assert(ReadVarValUnsigned(), $"VarValU{VariableValueSize}", "0x{0:X}", option);

        public ulong AssertVarValUnsigned(ReadOnlySpan<ulong> options)
            => Assert(ReadVarValUnsigned(), $"VarValU{VariableValueSize}", "0x{0:X}", options);

        public double AssertVarValPrecise(double option)
            => Assert(ReadVarValUnsigned(), $"VarValF{VariableValueSize}", "0x{0:X}", option);

        public double AssertVarValPrecise(ReadOnlySpan<double> options)
            => Assert(ReadVarValUnsigned(), $"VarValF{VariableValueSize}", "0x{0:X}", options);

        #endregion
    }
}
