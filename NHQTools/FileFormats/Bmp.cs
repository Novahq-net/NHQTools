using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Extensions;

// ReSharper disable IdentifierTypo

namespace NHQTools.FileFormats
{
    public static class Bmp
    {
        // Public
        public const int HeaderLen = 54;
        public const int MinExpectedLen = HeaderLen + 2;

        public static readonly int[] SupportedBpp = { 1, 4, 8, 16, 24, 32 };

        public const int HeaderFileSizeOffset = 2;
        public const int HeaderDibOffset = 14;
        public const int HeaderColorPlanesOffset = 26;
        public const int HeaderBppOffset = 28;

        public const int BITMAPINFOHEADER_SZ = 40;
        public const int OS21XBITMAPHEADER_SZ = 12;

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Bmp() => Def = Definitions.GetFormatDef(FileType.BMP);

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToBmp

        // Yes we do BMP to BMP here because we need to validate the data from NovaLogic files
        public static Bitmap ToBmp(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists 
                ? throw new FileNotFoundException("File not found.", file.FullName) 
                : ToBmp(File.ReadAllBytes(file.FullName));
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static Bitmap ToBmp(byte[] imgData)
        {
            if (imgData == null || imgData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            var magic = imgData.ReadBytes(0, Def.MagicBytes.Length);

            // Validate Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected {Def.MagicBytes.ToHex()}, got {magic.ToHex()}");

            var readSize = (uint)imgData.ReadInt32Le(HeaderFileSizeOffset);
            var expectedSize = (uint)imgData.Length;

            if (readSize != expectedSize)
            {
                // Prevent mutating the original array if we need to fix the header
                imgData = (byte[])imgData.Clone();

                // Correct the file size in the header so Bitmap does not complain
                imgData[2] = (byte)expectedSize;
                imgData[3] = (byte)(expectedSize >> 8);
                imgData[4] = (byte)(expectedSize >> 16);
                imgData[5] = (byte)(expectedSize >> 24);
            }

            using (var stream = new MemoryStream(imgData))
            using (var bmp = new Bitmap(stream))
                return new Bitmap(bmp);

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        // We need this to validate BMP data extracted from NovaLogic files as they don't always have correct file size in header
        // All known samples have a standard 14-byte file header + 40-byte DIB header
        // We read offset 14 to get the DIB header size (should be 40), plus a few other checks
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            if (!data.StartsWith(Def.MagicBytes))
                return false;

            // Read the DIB header length at offset 14 (4 bytes)
            // It should be 40 for Windows BITMAPINFOHEADER
            // It should be 12 for OS21XBITMAPHEADER
            var dibLen = data.ReadInt32Le(HeaderDibOffset);

            if (dibLen != BITMAPINFOHEADER_SZ && dibLen != OS21XBITMAPHEADER_SZ)
                return false;

            // Read the file size from the BMP header at offset 2 (4 bytes)
            // If these few checks pass, we can be reasonably sure it's a BMP file
            // if the file size matches the actual data length
            var fileSize = data.ReadInt32Le(HeaderFileSizeOffset);

            if (fileSize == data.Length)
                return true;

            // Read the number of color planes at offset 26 (2 bytes)
            // Spec says this must be 1
            if (data.ReadUInt16Le(HeaderColorPlanesOffset) != 1)
                return false;

            // Read the bits per pixel at offset 28 (2 bytes)
            var bpp = (int)data.ReadUInt16Le(HeaderBppOffset);

            // YOLO accept common bpp values
            return Array.IndexOf(SupportedBpp, bpp) >= 0;
        }
        #endregion

    }

}