using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

// ReSharper disable IdentifierTypo

namespace NHQTools.FileFormats
{
    public static class Dds
    {

        // Public
        public const int HeaderLen = 128;
        public const int MinExpectedLen = HeaderLen + 2;

        // Constants
        private const int DDPF_ALPHAPIXELS = 0x00000001;
        private const int DDPF_FOURCC = 0x00000004;
        private const int DDPF_RGB = 0x00000040;

        private const uint FOURCC_DXT1 = 0x31545844;
        private const uint FOURCC_DXT3 = 0x33545844;
        private const uint FOURCC_DXT5 = 0x35545844;

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Dds() => Def = Definitions.GetFormatDef(FileType.DDS);

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
            var bgra = DecodeDds(imgData, false, out var width, out var height);
            return Images.CreateBmp(bgra, width, height);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToPng

        // Converts DDS file data to a PNG byte array.
        // When alpha is true, the decoded alpha channel is preserved (DXT1 punch-through,
        // DXT3 explicit alpha, DXT5 interpolated alpha, or uncompressed RGBA).
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
            var bgra = DecodeDds(imgData, alpha, out var width, out var height);
            return Images.CreatePng(bgra, width, height);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decode

        // Decodes DDS image data into a raw BGRA byte array.
        // When alpha is false, all alpha values are set to 255 (fully opaque).
        private static byte[] DecodeDds(byte[] imgData, bool alpha, out int width, out int height)
        {
            if (imgData == null || imgData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            var reader = new ByteReader(imgData);

            var magic = reader.ReadBytes(Def.MagicBytes.Length);

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            // Header (124 bytes)
            var headerSize = reader.ReadInt32(); // dwSize (should be 124)

            reader.Skip(4); // flags

            height = reader.ReadInt32();
            width = reader.ReadInt32();

            reader.Skip(12); //pitchOrLinearSize, depth, mipMapCount 
            reader.Skip(44); // reserved1[11]

            // Validate dimensions
            if (width <= 0 || height <= 0)
                throw new InvalidDataException($"Invalid DDS dimensions: {width}x{height}.");

            // Pixel Format Structure (32 bytes)
            reader.Skip(4); // pfSize size of pixel format struct (32)

            var pfFlags = reader.ReadInt32();
            var pfFourCc = reader.ReadUInt32();
            var pfRgbBitCount = reader.ReadInt32();
            var pfRBitMask = reader.ReadUInt32();
            var pfGBitMask = reader.ReadUInt32();
            var pfBBitMask = reader.ReadUInt32();
            var pfABitMask = reader.ReadUInt32();

            // Jump to the absolute start of the data.
            // Data Start = Magic(4) + HeaderSize(124)
            reader.Position = 4 + headerSize;

            // Output
            var outSize = (long)width * height * 4;

            if (outSize > int.MaxValue)
                throw new InvalidDataException("Image dimensions are too large.");

            var outImg = new byte[outSize];

            // Decode
            if ((pfFlags & DDPF_FOURCC) != 0)
            {
                // Validate supported FourCC
                if (pfFourCc != FOURCC_DXT1 && pfFourCc != FOURCC_DXT3 && pfFourCc != FOURCC_DXT5)
                    throw new NotSupportedException($"Unsupported DDS FourCC format: 0x{pfFourCc:X8}");

                // Compressed (DXT1, DXT3, DXT5)
                var blockCountX = (width + 3) / 4;
                var blockCountY = (height + 3) / 4;

                // Pre-allocate reusable buffers to avoid per-block heap allocations
                var colors = new Color[4];
                var alphas = new int[8];
                var alphaBlock = new byte[8];
                var colorBlock = new byte[8];

                for (var y = 0; y < blockCountY; y++)
                {
                    for (var x = 0; x < blockCountX; x++)
                        DecompressBlock(reader, x * 4, y * 4, width, height, outImg, pfFourCc,
                            colors, alphas, alphaBlock, colorBlock);

                }
            }
            else if ((pfFlags & DDPF_RGB) != 0)
            {
                // Uncompressed RGB/RGBA
                if (pfRgbBitCount != 32)
                    throw new NotSupportedException($"Uncompressed DDS bit depth {pfRgbBitCount} not supported.");

                var hasAlpha = (pfFlags & DDPF_ALPHAPIXELS) != 0;

                // Compute channel shifts from bit masks
                var rShift = GetShift(pfRBitMask);
                var gShift = GetShift(pfGBitMask);
                var bShift = GetShift(pfBBitMask);
                var aShift = hasAlpha ? GetShift(pfABitMask) : 0;

                for (var i = 0; i < width * height; i++)
                {
                    var pixel = reader.ReadUInt32();

                    var r = (byte)((pixel & pfRBitMask) >> rShift);
                    var g = (byte)((pixel & pfGBitMask) >> gShift);
                    var b = (byte)((pixel & pfBBitMask) >> bShift);
                    var a = hasAlpha ? (byte)((pixel & pfABitMask) >> aShift) : (byte)255;

                    // Write as BGRA
                    var offset = i * 4;
                    outImg[offset + 0] = b;
                    outImg[offset + 1] = g;
                    outImg[offset + 2] = r;
                    outImg[offset + 3] = a;
                }

            }
            else
            {
                throw new NotSupportedException("Unsupported DDS Pixel Format.");
            }

            // Strip alpha channel when not requested
            if (alpha) 
                return outImg;

            for (var i = 3; i < outImg.Length; i += 4)
                outImg[i] = 255;
        
            return outImg;
        }

        // Decompress a 4x4 Block
        private static void DecompressBlock(ByteReader reader, int x, int y, int width, int height,
            byte[] output, uint format, Color[] colors, int[] alphas, byte[] alphaBlock, byte[] colorBlock)
        {
            var hasAlphaBlock = false;

            // DXT3 and DXT5 have an alpha block before the color block
            if (format == FOURCC_DXT3 || format == FOURCC_DXT5)
            {
                reader.ReadBytes(alphaBlock, 0, 8);
                hasAlphaBlock = true;
            }

            // Read Color Block (8 bytes)
            reader.ReadBytes(colorBlock, 0, 8);

            // Decode Color Endpoints (Little Endian)
            var c0 = BitConverter.ToUInt16(colorBlock, 0);
            var c1 = BitConverter.ToUInt16(colorBlock, 2);

            // Expand 565 to 888
            colors[0] = Rgb565ToColor(c0);
            colors[1] = Rgb565ToColor(c1);

            if (format == FOURCC_DXT1 && c0 <= c1)
            {
                // DXT1 Transparent Mode
                colors[2] = InterpolateColor(colors[0], colors[1], 1, 1);
                colors[3] = Color.FromArgb(0, 0, 0, 0);
            }
            else
            {
                // Standard Interpolation
                colors[2] = InterpolateColor(colors[0], colors[1], 2, 1);
                colors[3] = InterpolateColor(colors[0], colors[1], 1, 2);
            }

            // Lookup Table
            var lookupTable = BitConverter.ToUInt32(colorBlock, 4);

            // Pre-compute DXT5 alpha palette
            ulong alphaLookup = 0;

            if (format == FOURCC_DXT5 && hasAlphaBlock)
            {
                var a0 = alphaBlock[0];
                var a1 = alphaBlock[1];

                // 48-bit lookup table starts at byte 2
                alphaLookup = BitConverter.ToUInt64(alphaBlock, 0) >> 16;

                // Calculate Alpha Palette
                alphas[0] = a0;
                alphas[1] = a1;

                if (a0 > a1)
                {
                    for (var i = 2; i < 8; i++)
                        alphas[i] = ((8 - i) * a0 + (i - 1) * a1) / 7;
                }
                else
                {
                    for (var i = 2; i < 6; i++)
                        alphas[i] = ((6 - i) * a0 + (i - 1) * a1) / 5;

                    alphas[6] = 0;
                    alphas[7] = 255;
                }
            }

            // Process 4x4 Pixels
            for (var blockY = 0; blockY < 4; blockY++)
            {
                for (var blockX = 0; blockX < 4; blockX++)
                {
                    // Check boundaries
                    if (x + blockX >= width || y + blockY >= height)
                        continue;

                    // Calculate color index (2 bits per pixel)
                    // Row-major packing: (row * 4 + col) * 2
                    var shift = (blockY * 4 + blockX) * 2;
                    var colorIndex = (lookupTable >> shift) & 0x03;

                    var finalColor = colors[colorIndex];

                    // DXT1: Alpha is embedded in the color table (colors[3].A = 0 for transparent)
                    int alpha = finalColor.A;

                    switch (format)
                    {
                        // Alpha Overlay (DXT3/DXT5)
                        case FOURCC_DXT3:
                            {
                                // DXT3: Explicit 4-bit alpha (16 nibbles)
                                if (hasAlphaBlock)
                                {
                                    var nibbleIndex = blockY * 4 + blockX;
                                    var byteIndex = nibbleIndex / 2;
                                    int val = alphaBlock[byteIndex];
                                    var alpha4 = (nibbleIndex % 2 == 0) ? (val & 0x0F) : ((val >> 4) & 0x0F);
                                    alpha = alpha4 * 17; // Scale 0-15 to 0-255
                                }

                                break;
                            }
                        case FOURCC_DXT5:
                            {
                                // DXT5: Interpolated Alpha (palette pre-computed above)
                                if (!hasAlphaBlock)
                                    break;

                                // Get 3-bit index for this pixel
                                var bitOffset = (blockY * 4 + blockX) * 3;
                                var alphaIndex = (int)((alphaLookup >> bitOffset) & 0x07);

                                alpha = alphas[alphaIndex];

                                break;
                            }
                    }

                    // Write to Output (BGRA)
                    var pixelIndex = ((y + blockY) * width + (x + blockX)) * 4;
                    output[pixelIndex + 0] = finalColor.B;
                    output[pixelIndex + 1] = finalColor.G;
                    output[pixelIndex + 2] = finalColor.R;
                    output[pixelIndex + 3] = (byte)alpha;
                }

            }

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Helpers

        // Returns the bit shift for a given bitmask (position of the lowest set bit).
        private static int GetShift(uint mask)
        {
            if (mask == 0)
                return 0;

            var shift = 0;

            while ((mask & 1) == 0)
            {
                mask >>= 1;
                shift++;
            }

            return shift;
        }

        // Converts a 16-bit RGB565 color value to a fully opaque 32-bit ARGB color.
        private static Color Rgb565ToColor(ushort val)
        {
            var r = (val & 0xF800) >> 11;
            var g = (val & 0x07E0) >> 5;
            var b = (val & 0x001F);

            // Scale to 8-bit
            r = (r << 3) | (r >> 2);
            g = (g << 2) | (g >> 4);
            b = (b << 3) | (b >> 2);

            return Color.FromArgb(255, r, g, b);
        }

        // Calculates a weighted average of two colors and returns the resulting interpolated color.
        private static Color InterpolateColor(Color c1, Color c2, int w1, int w2)
        {
            var r = (c1.R * w1 + c2.R * w2) / (w1 + w2);
            var g = (c1.G * w1 + c2.G * w2) / (w1 + w2);
            var b = (c1.B * w1 + c2.B * w2) / (w1 + w2);

            // Alpha is always set to 255
            return Color.FromArgb(255, r, g, b);
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

            // dwSize should be 124
            var headerSize = data.PeekInt32(4);
            if (headerSize != 124)
                return false;

            // Dimensions must be positive
            var height = data.PeekInt32(12);
            var width = data.PeekInt32(16);
            return width > 0 && height > 0;
        }
        #endregion

    }

}