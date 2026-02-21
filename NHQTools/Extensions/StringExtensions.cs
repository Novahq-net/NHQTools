using System;
using System.Text;
using System.Globalization;

// ReSharper disable MethodOverloadWithOptionalParameter

namespace NHQTools.Extensions
{
    public static class StringExtensions
    {
        private static readonly byte[] EmptyByte = Array.Empty<byte>();

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool ArrayContains(this string[] array, string value, StringComparison comparison = StringComparison.Ordinal)
        {
            if (array == null || array.Length == 0 || value == null)
                return false;

            foreach (var t in array)
            {
                if (string.Equals(t, value, comparison))
                    return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////////////////////
        public static int GetByteCount(this string str, Encoding enc, int addNullByte = 1) => (str == null) ? addNullByte : enc.GetByteCount(str) + addNullByte;

        ////////////////////////////////////////////////////////////////////////////////////
        #region Converters
        public static byte[] ToBytes(this string str, Encoding enc = null)
        {
            enc = enc ?? Encoding.ASCII;
            return str == null ? EmptyByte : enc.GetBytes(str);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static uint ToFloatBits(this string str, NumberStyles? numberStyle = null, IFormatProvider formatProvider = null)
        {
            if (str == null)
                return 0;

            numberStyle = numberStyle ?? NumberStyles.Float;
            formatProvider = formatProvider ?? CultureInfo.InvariantCulture;

            // float.TryParse often drops the negative sign on zero, resulting in 0x00000000.
            // Force the correct IEEE 754 bit pattern for -0.0 (0x80000000).
            if (str == "-0.0")
                return 0x80000000;

            // InvariantCulture ensures '.' is treated as decimal, not ','
            return float.TryParse(str, numberStyle.Value, formatProvider, out var fVal)
                ? BitConverter.ToUInt32(BitConverter.GetBytes(fVal), 0)
                : 0;
        }

        public static int ToFloatBits(this string str, NumberStyles? numberStyle = null, IFormatProvider formatProvider = null, bool asInt = true)
            => (int)str.ToFloatBits(numberStyle, formatProvider);

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool TryParseFloat(this string str, NumberStyles numberStyle, IFormatProvider formatProvider, out uint resultBits)
        {
            resultBits = 0;

            if (string.IsNullOrEmpty(str))
                return false;

            // Check float-like string
            var hasFloatIndicators = str.Contains(".") ||
                                      str.IndexOf("E", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      str.IndexOf("NaN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      str.EndsWith("Infinity", StringComparison.OrdinalIgnoreCase) ||
                                      str == "-0.0";

            if (!hasFloatIndicators)
                return false;

            // Check if the string is actually a valid number
            // We must do this before calling ToFloat because ToFloat returns '0' on failure,
            // which creates ambiguity. We wouldn't know if it failed or if the value was actually 0.0
            if (!float.TryParse(str, numberStyle, formatProvider, out _))
                return false;

            // Use ToFloat to ensure the -0.0 case is handled uniformly
            resultBits = str.ToFloatBits(numberStyle, formatProvider);
            return true;
        }

        public static bool TryParseFloat(this string str, NumberStyles numberStyle, IFormatProvider formatProvider, out int resultBits)
        {
            if (str.TryParseFloat(numberStyle, formatProvider, out uint uBits))
            {
                resultBits = (int)uBits;
                return true;
            }

            resultBits = 0;
            return false;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Padding
        public static string RPad(this string str, int length, char padChar = '\0', bool nullTerminator = true)
        {
            str = str ?? string.Empty;

            var limit = nullTerminator ? length - 1 : length;

            if (str.Length > limit)
                str = str.Substring(0, limit);

            return str.PadRight(length, padChar);
        }

        public static byte[] RPad(this string str, int length, Encoding enc, bool nullTerminator = true)
        {
            var b = enc.GetBytes(str ?? string.Empty);
            return b.RPad(length, nullTerminator);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Escape/Unescape
        public static string EscapeQuotes(this string str) => str?.Replace("\"", "\\\"");

        ////////////////////////////////////////////////////////////////////////////////////
        public static string UnescapeQuotes(this string s) => s?.Replace("\\\"", "\"");

        #endregion

        #region Replace with StringComparison

        public static string Replace(this string str, string oldValue, string newValue, StringComparison comparisonType)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str), "String cannot be null.");

            if (string.IsNullOrEmpty(oldValue))
                throw new ArgumentException("String cannot be null or empty.", nameof(oldValue));

            if (str.Length == 0)
                return str;

            var sb = new StringBuilder(str.Length);

            var startIndex = 0;
            int foundAt;

            while ((foundAt = str.IndexOf(oldValue, startIndex, comparisonType)) >= 0)
            {
                sb.Append(str, startIndex, foundAt - startIndex);

                if (!string.IsNullOrEmpty(newValue))
                    sb.Append(newValue);

                startIndex = foundAt + oldValue.Length;
            }

            sb.Append(str, startIndex, str.Length - startIndex);

            return sb.ToString();
        }
        #endregion

    }

}