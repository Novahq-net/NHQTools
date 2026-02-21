using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class Tga
    {
        // Public
        public const int HeaderLen = 18; // v1 header length
        public const int MinExpectedLen = HeaderLen + 1;

        // Supported TGA types and bpp
        private static readonly int[] SupportedTypes = { 1, 2, 9, 10 }; // 3 and 11 are greyscale and not covered
        private static readonly int[] SupportedBpp = { 8, 15, 16, 24, 32 };

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        private static readonly byte[] V2SigBytes = DefaultEnc.GetBytes("TRUEVISION-XFILE.\0"); // 0x54, 0x52, 0x55, 0x45, 0x56, 0x49, 0x53, 0x49, 0x4F, 0x4E, 0x2D, 0x58, 0x46, 0x49, 0x4C, 0x45
        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Tga() => Def = Definitions.GetFormatDef(FileType.TGA);

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
            var (bgra, width, height, rotate) = DecodeTga(imgData, false);
            return Images.CreateBmp(bgra, width, height, rotateFlip: rotate);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToPng

        // Converts TGA file data to a PNG byte array.
        // When alpha is true, the decoded alpha channel is preserved
        // (32-bit BGRA, 16-bit attribute bit, or palette alpha).
        // When alpha is false, all pixels are returned fully opaque.
        public static byte[] ToPng(FileInfo file, bool alpha = false)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : ToPng(File.ReadAllBytes(file.FullName), alpha);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static byte[] ToPng(byte[] imgData, bool alpha = false)
        {
            var (bgra, width, height, rotate) = DecodeTga(imgData, alpha);
            return Images.CreatePng(bgra, width, height, rotateFlip: rotate);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decode

        // Decodes TGA image data into a raw BGRA byte array.
        // When alpha is false, all alpha values are set to 255 (fully opaque).
        private static (byte[] bgra, int width, int height, RotateFlipType rotate) DecodeTga(byte[] imgData, bool alpha)
        {
            if (imgData == null || imgData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            if (!Validator(null, imgData))
                throw new InvalidDataException("Data is not a valid TGA image.");

            var reader = new ByteReader(imgData);

            var idLen = reader.ReadByte();
            var cMapType = reader.ReadByte();
            var imageType = reader.ReadByte(); // 1=Map, 2=RGB, 9=RLE Map, 10=RLE RGB

            reader.Skip(2); // colorMapOrigin

            var cMapLen = reader.ReadUInt16();
            var cMapDepth = reader.ReadByte();

            reader.Skip(4);    // xOrigin / yOrigin

            var width = reader.ReadUInt16();
            var height = reader.ReadUInt16();

            var bpp = reader.ReadByte();
            var descriptor = reader.ReadByte();

            // Validate supported types (Only 1, 2, 9, 10 are covered)
            if (Array.IndexOf(SupportedTypes, imageType) < 0)
                throw new NotSupportedException($"TGA Image Type {imageType:X} is not supported.");

            // Skip Image Id Fields if present
            if (idLen > 0)
                reader.Skip(idLen);

            // Read Color Map (Palette) if present
            byte[] palette = null;
            if (cMapType == 1 && cMapLen > 0)
            {
                // Calculate bytes per entry (usually 2, 3, or 4)
                var bytesPerEntry = cMapDepth / 8;

                palette = reader.ReadBytes(cMapLen * bytesPerEntry);
            }

            // Output Buffer
            // Convert to 32-bit BGRA (Format32bppArgb) for WinForms compatibility
            var outImg = new byte[width * height * 4];
            var totalPixels = width * height;
            var currentPixel = 0;

            // Decode Image Data
            var isRle = (imageType == 9 || imageType == 10);
            var bytesPerPixel = (bpp + 7) / 8;

            while (currentPixel < totalPixels)
            {

                if (!isRle) // Uncompressed
                {
                    var pixel = ReadPixel(reader, bytesPerPixel, palette, cMapDepth);
                    WritePixel(outImg, currentPixel++, pixel);
                    continue;
                }

                // RLE Packet Header
                var packetHeader = reader.ReadByte();
                var count = (packetHeader & 0x7F) + 1;
                var isRlePacket = (packetHeader & 0x80) != 0;

                // Prevent overruns in case of bad rle value
                var remaining = totalPixels - currentPixel;
                if (count > remaining)
                    count = remaining;

                if (isRlePacket) // Run-length packet
                {
                    var pixel = ReadPixel(reader, bytesPerPixel, palette, cMapDepth);

                    for (var i = 0; i < count; i++)
                        WritePixel(outImg, currentPixel++, pixel);
                }
                else // Raw packet
                {
                    for (var i = 0; i < count; i++)
                    {
                        var pixel = ReadPixel(reader, bytesPerPixel, palette, cMapDepth);
                        WritePixel(outImg, currentPixel++, pixel);
                    }
                }

            }

            var rotate = (descriptor & 0x20) == 0 ? RotateFlipType.RotateNoneFlipY : RotateFlipType.RotateNoneFlipNone;

            // Strip alpha channel when not requested
            if (!alpha)
            {
                for (var i = 3; i < outImg.Length; i += 4)
                    outImg[i] = 255;
            }

            return (outImg, width, height, rotate);
        }

        // Reads a single pixel from stream or palette
        private static byte[] ReadPixel(ByteReader reader, int bpp, byte[] palette, int paletteEntryLen)
        {

            // RGB Image (Type 2 or 10): Pixel data is direct color 
            if (palette == null)
                return reader.ReadBytes(bpp);

            // Mapped Image (Type 1 or 9): Pixel data is an index into the palette
            int index = (bpp == 1) ? reader.ReadByte() : reader.ReadUInt16();

            // Retrieve color from palette
            var paletteBytes = paletteEntryLen / 8;
            var color = new byte[paletteBytes];

            Array.Copy(palette, index * paletteBytes, color, 0, paletteBytes);

            return color;
        }

        // Writes a single pixel in 32-bit BGRA format
        private static void WritePixel(byte[] buffer, int index, byte[] pixelData)
        {
            if (index * 4 >= buffer.Length)
                return;

            // Defaults
            byte r = 0;
            byte g = 0;
            byte b = 0;
            byte a = 255;

            switch (pixelData.Length)
            {
                // 24-bit (BGR)
                case 3:
                    b = pixelData[0];
                    g = pixelData[1];
                    r = pixelData[2];
                    break;
                // 32-bit (BGRA)
                case 4:
                    b = pixelData[0];
                    g = pixelData[1];
                    r = pixelData[2];
                    a = pixelData[3];
                    break;
                // 16-bit (ARRRRRGG GGGBBBBB)
                case 2:
                    {
                        // Stored lo-hi: GGGBBBBB (0), ARRRRRGG (1)
                        var val = BitConverter.ToUInt16(pixelData, 0);

                        // Extract 5-bit channels
                        b = (byte)((val & 0x001F) << 3);       // Bits 0-4
                        g = (byte)((val & 0x03E0) >> 2);       // Bits 5-9 (shifted down 5, up 3)
                        r = (byte)((val & 0x7C00) >> 7);       // Bits 10-14
                        a = (val & 0x8000) != 0 ? (byte)255 : (byte)0; // Bit 15 is Attribute/Alpha
                        break;
                    }
                // 8-bit Greyscale
                case 1:
                    b = g = r = pixelData[0];
                    break;
            }

            var offset = index * 4;
            buffer[offset + 0] = b;
            buffer[offset + 1] = g;
            buffer[offset + 2] = r;
            buffer[offset + 3] = a;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format

        internal static bool Validator(string fileName, byte[] data) => IsV2(data) || IsV1(data);

        ////////////////////////////////////////////////////////////////////////////////////
        private static bool IsV1(byte[] data)
        {

            if (data.Length < MinExpectedLen)
                return false;

            var reader = new ByteReader(data, DefaultEnc);

            var idLen = reader.ReadByte();
            var colorMapType = reader.ReadByte();
            var imageType = reader.ReadByte();

            reader.Skip(2); // colorMapOrigin

            var colorMapLen = reader.ReadUInt16();
            var cMapDepth = reader.ReadByte();

            reader.Skip(4);    // xOrigin / yOrigin

            var width = reader.ReadUInt16();
            var height = reader.ReadUInt16();
            var bpp = reader.ReadByte();

            reader.Skip(1); // descriptor

            if (width <= 0 || height <= 0)
                return false;

            if (colorMapType > 1)
                return false;

            // Validate supported types
            if (Array.IndexOf(SupportedTypes, imageType) < 0)
                return false;

            if (Array.IndexOf(SupportedBpp, bpp) < 0)
                return false;

            // Calculate color map size
            long colorMapBytes = 0;
            if (colorMapType == 1)
            {
                var entryBytes = (cMapDepth + 7) / 8;
                if (entryBytes <= 0)
                    return false;

                colorMapBytes = (long)colorMapLen * entryBytes;
            }

            // RLE types have variable size, so we can't validate their full size easily without decoding
            if (imageType == 9 || imageType == 10)
                return true;

            // Calculate where the image data starts
            var imgDataOffset = 18L + idLen + colorMapBytes;

            // Boundary check
            if (imgDataOffset > data.Length)
                return false;

            long bytesPerPixel = (bpp + 7) / 8;

            // For Type 1 or 2, the size must match or be larger than expected
            var uncompressedSize = (long)width * height * bytesPerPixel;
            var expectedEnd = imgDataOffset + uncompressedSize;

            return expectedEnd <= data.Length;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static bool IsV2(byte[] data)
        {
            if (data.Length < MinExpectedLen + V2SigBytes.Length)
                return false;

            var sigOffset = data.Length - V2SigBytes.Length;

            return data.Matches(V2SigBytes, sigOffset);
        }
        #endregion 

    }

}