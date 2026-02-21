using System;

namespace NHQTools.Extensions
{
    public static class DateTimeExtensions
    {

        // Unix epoch start time (January 1, 1970, 00:00:00 UTC)
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Converts the specified date and time to a Unix timestamp
        public static uint ToUnixTimestamp(this DateTime date)
        {
            // Ensure UTC to avoid timezone offset errors
            if (date.Kind == DateTimeKind.Local)
                date = date.ToUniversalTime();

            var diff = date - Epoch;
            var seconds = Math.Max(0, diff.TotalSeconds);

            return seconds > uint.MaxValue ? uint.MaxValue : (uint)seconds;
        }

    }

}