using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class R16
    {

        // Public
        public const int HeaderLen = 4;
        public const int MinExpectedLen = HeaderLen + 6;

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static R16() => Def = Definitions.GetFormatDef(FileType.R16);

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToBmp
        // Converts a 16-bit grayscale image to a 32-bit BGRA bitmap.
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

            var reader = new ByteReader(imgData);

            var magic = reader.ReadBytes(Def.MagicBytes.Length);

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            var width = reader.ReadInt32();
            var pixelDataStart = HeaderLen;
            var height = (imgData.Length - pixelDataStart) / 2 / width;
     
            // Validate Dimensions
            if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
                throw new InvalidDataException($"Unexpected image dimensions: Width: {width} Height: {height}");

            var expectedBytes = pixelDataStart + (width * height * 2);

            if (imgData.Length < expectedBytes)
                throw new InvalidDataException($"Data too short: expected {expectedBytes} bytes, got {imgData.Length}.");

            // Decode R16 to standard 32-bit BGRA array
            // Convert to 32-bit (4 bytes per pixel)
            var outImg = new byte[checked(width * height * 4)];
            var pixelPtr = 0;

            for (var y = 0; y < height; y++)
            {
                var rowStart = pixelDataStart + (y * width * 2);

                for (var x = 0; x < width; x++)
                {
                    var srcIdx = rowStart + (x * 2);

                    if (srcIdx + 1 >= imgData.Length) 
                        break;

                    // Read 16-bit pixel
                    var val = (ushort)(imgData[srcIdx] | (imgData[srcIdx + 1] << 8));

                    // Convert to 8-bit Gray (high byte)
                    var gray = (byte)(val >> 8);

                    // Write BGRA (Blue, Green, Red, Alpha)
                    outImg[pixelPtr++] = gray; // B
                    outImg[pixelPtr++] = gray; // G
                    outImg[pixelPtr++] = gray; // R
                    outImg[pixelPtr++] = 255;  // A
                }

            }

            return Images.CreateBmp(outImg, width, height);

        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            if (!data.StartsWith(Def.MagicBytes))
                return false;

            // Width must be positive and pixel data must fit
            var width = data.PeekInt32(4);
            if (width <= 0 || width > 8192)
                return false;

            var pixelDataLen = data.Length - HeaderLen;
            return pixelDataLen >= width * 2;
        }
        #endregion

    }

}