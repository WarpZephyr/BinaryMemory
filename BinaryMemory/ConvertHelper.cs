namespace BinaryMemory
{
    /// <summary>
    /// A conversion helper for defining conversions in methods that can be passed as delegates.
    /// </summary>
    internal static class ConvertHelper
    {
        /// <summary>
        /// Converts a <see cref="Half"/> into a <see cref="double"/>.
        /// </summary>
        /// <param name="value">A <see cref="Half"/>.</param>
        /// <returns>A <see cref="double"/>.</returns>
        internal static double ToDouble(Half value)
        {
            return (double)value;
        }
    }
}
