
namespace NHQTools.Extensions
{
    public static class NumberExtensions
    {

        ////////////////////////////////////////////////////////////////////////////////
        #region ToFileSize Units
        // { "B", "KB", "MB", "GB", "TB", "PB" };
        private static readonly string[] DefaultToFileSizeUnits = { "B", "KB", "MB" };

        // Converts a byte count to a human-readable file size string
        public static string ToFileSize(this long bytes, bool toLower = false, string[] units = null)
        {
            var fsUnits = units ?? DefaultToFileSizeUnits;
            var lastIdx = fsUnits.Length - 1;
            var index = 0;
            var divisor = 1L;

            while (index < lastIdx && bytes >= divisor * 1024)
            {
                divisor *= 1024;
                index++;
            }

            var outUnit = toLower ? fsUnits[index].ToLower() : fsUnits[index];

            // always a whole number, no decimal needed
            if (index == 0)
                return string.Concat(bytes.ToString("N0"), outUnit);

            // compute one fractional digit
            var intPart = bytes / divisor;
            var tenths = (int)((bytes % divisor) * 10 / divisor);

            return tenths == 0
                ? string.Concat(intPart.ToString("N0"), outUnit)
                : string.Concat(intPart.ToString("N0"), ".", tenths.ToString(), outUnit);
        }
        public static string ToFileSize(this int bytes, bool toLower = false, string[] units = null)
            => ((long)bytes).ToFileSize(toLower, units);

        public static string ToFileSize(this uint bytes, bool toLower = false, string[] units = null)
            => ((long)bytes).ToFileSize(toLower, units);
        #endregion

    }

}