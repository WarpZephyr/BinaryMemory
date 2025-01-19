using System;
using System.IO;
using System.Linq;

namespace BinaryMemory
{
    public static class AssertHelper
    {
        public static void Assert<T>(T value, string typeName, string valueFormat, ReadOnlySpan<T> options) where T : IEquatable<T>
        {
            foreach (T option in options)
            {
                if (value.Equals(option))
                {
                    return;
                }
            }

            string strValue = string.Format(valueFormat, value);
            string strOptions = string.Join(", ", options.ToArray().Select(o => string.Format(valueFormat, o)));
            throw new InvalidDataException($"Assertion failed for {typeName}: {strValue} | Expected: {strOptions}");
        }

        public static void Assert<T>(T value, string typeName, string valueFormat, T option) where T : IEquatable<T>
        {
            if (value.Equals(option))
            {
                return;
            }

            string strValue = string.Format(valueFormat, value);
            string strOption = string.Format(valueFormat, option);
            throw new InvalidDataException($"Assertion failed for {typeName}: {strValue} | Expected: {strOption}");
        }

        public static void Assert<T>(T value, string typeName, ReadOnlySpan<T> options) where T : IEquatable<T>
        {
            foreach (T option in options)
            {
                if (value.Equals(option))
                {
                    return;
                }
            }

            string strOptions = string.Join(", ", options.ToArray());
            throw new InvalidDataException($"Assertion failed for {typeName}: {value} | Expected: {strOptions}");
        }

        public static void Assert<T>(T value, string typeName, T option) where T : IEquatable<T>
        {
            if (value.Equals(option))
            {
                return;
            }

            throw new InvalidDataException($"Assertion failed for {typeName}: {value} | Expected: {option}");
        }

        public static void Assert<T>(T value, ReadOnlySpan<T> options) where T : IEquatable<T>
        {
            foreach (T option in options)
            {
                if (value.Equals(option))
                {
                    return;
                }
            }

            string strOptions = string.Join(", ", options.ToArray());
            throw new InvalidDataException($"Assertion failed for value: {value} | Expected: {strOptions}");
        }

        public static void Assert<T>(T value, T option) where T : IEquatable<T>
        {
            if (value.Equals(option))
            {
                return;
            }

            throw new InvalidDataException($"Assertion failed for value: {value} | Expected: {option}");
        }
    }
}
