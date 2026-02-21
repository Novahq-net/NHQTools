using System;
using System.IO;
using System.Collections.Generic;

namespace NHQTools.Extensions
{
    public static class TextReaderExtensions
    {
        public static IEnumerable<string> Lines(this TextReader reader, bool trim = false, char[] trimChars = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader), "Reader cannot be null.");

            string line;
            while ((line = reader.ReadLine()) != null)
                yield return trim
                    ? line.Trim(trimChars != null && trimChars.Length > 0 ? trimChars : null)
                    : line;

        }

    }

}