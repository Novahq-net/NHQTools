using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class Jpg
    {
        // Public
        public const int HeaderLen = 16;
        public const int MinExpectedLen = HeaderLen + 2;

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Jpg() => Def = Definitions.GetFormatDef(FileType.JPG);

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToBmp
        public static Bitmap ToBmp(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : ToBmp(File.ReadAllBytes(file.FullName));
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static Bitmap ToBmp(byte[] imgData)
        {
            if (imgData == null || imgData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            // Verify Magic Bytes
            var magic = imgData.ReadBytes(0, Def.MagicBytes.Length);

            // Verify Magic Bytes (NovaLogic JPGs use JFIF markers only)
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected {Def.MagicBytes.ToHex()}, got {magic.ToHex()}");

            using (var stream = new MemoryStream(imgData))
            using (var bmp = new Bitmap(stream))
                return new Bitmap(bmp);

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            // We only care about JFIF marker since we expect JFIF for NovaLogic JPGs
            return data.StartsWith(Def.MagicBytes);

        }
        #endregion

    }

}