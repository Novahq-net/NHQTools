using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Utilities;

namespace NHQTools.FileFormats
{
    public static class Txt
    {
        // Public
        public const int HeaderLen = 0;
        public const int MinExpectedLen = HeaderLen + 1;

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.GetEncoding(Common.NlCodepage);

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Txt() => Def = Definitions.GetFormatDef(FileType.TXT);

        ////////////////////////////////////////////////////////////////////////////////////
        #region To Text
        public static string ToTxt(FileInfo file, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : ToTxt(File.ReadAllBytes(file.FullName), format, enc);
        }

        public static string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (fileData == null || fileData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            return !Def.SerializeFormats.Contains(format) 
                ? throw new ArgumentException("Unsupported serialization format.", nameof(format)) 
                : enc.GetString(fileData);

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region From Text
        public static byte[] FromTxt(FileInfo file, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : FromTxt(File.ReadAllText(file.FullName, enc), format, enc);
        }
        
        ////////////////////////////////////////////////////////////////////////////////////
        public static byte[] FromTxt(string textData, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (textData == null || textData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            return !Def.SerializeFormats.Contains(format) 
                ? throw new ArgumentException("Unsupported serialization format.", nameof(format)) 
                : enc.GetBytes(textData);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Detection

        public static bool IsTxt(string fileName, byte[] data, bool[] forbiddenBytes, HashSet<string> excludedExtensions, int scanLimit = 4096)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data), "Data cannot be null.");

            if (forbiddenBytes == null || forbiddenBytes.Length < 256)
                throw new ArgumentException("Must be a 256-element lookup table.", nameof(forbiddenBytes));

            if (fileName != null && excludedExtensions != null)
            {
                var ext = Path.GetExtension(fileName);
                if (!string.IsNullOrEmpty(ext) && excludedExtensions.Contains(ext))
                    return false;
            }

            // Don't scan more than the data length
            scanLimit = Math.Min(data.Length, scanLimit);

            for (var i = 0; i < scanLimit; i++)
            {
                if (forbiddenBytes[data[i]])
                    return false;
            }

            return true;
        }
        #endregion

    }

}