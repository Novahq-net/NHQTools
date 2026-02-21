using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class Pcx
    {
        // Public
        public const int HeaderLen = 128;
        public const int MinExpectedLen = HeaderLen + 2;

        public static readonly int[] SupportedBpp = { 1, 8 };

        public const int PaletteLen = 256 * 3; // 768, RGB * 3
        public const int EgaPaletteLen = 16 * 3; // 48, RGB * 3
        public const int PaletteMarker = 0x0C;

        public enum ColorMode
        {
            Detect = -1,    // Auto-detect from palette and pixel/RLE data
            Rgb24,          // 8 bpp, 3 planes — 24-bit RGB (planar)
            Idx8,           // 8 bpp, 1 plane  — 256-color indexed
            Ega16,          // 1 bpp, 4 planes — 16-color EGA
            Mono            // 1 bpp, 1 plane  — Monochrome
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Pcx() => Def = Definitions.GetFormatDef(FileType.PCX);

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
            var bgra = DecodePcx(imgData, false, out var width, out var height);
            return Images.CreateBmp(bgra, width, height);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToPng

        // Converts PCX file data to a PNG byte array.
        // When alpha is true:
        //   Idx8  — palette index 0 is treated as fully transparent (key-color convention)
        //   Ega16 — palette index 0 is treated as fully transparent
        //   Mono  — black pixels (bit 0) are treated as fully transparent
        //   Rgb24 — no alpha convention exists; returned fully opaque
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
            var bgra = DecodePcx(imgData, alpha, out var width, out var height);
            return Images.CreatePng(bgra, width, height);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region ToPcx (Rebuild PCX)

        // Attempts to rebuild a full PCX file after extraction from various binary containers.
        // Accepts both RLE encoded and raw pixel data, and will auto-detect and decode RLE if needed.
        // Pixel format per mode:
        //   Rgb24 — 3 bytes per pixel (R, G, B), length = width * height * 3
        //   Idx8  — 1 byte per pixel (palette index 0–255), length = width * height
        //   Ega16 — 1 byte per pixel (color index 0–15), length = width * height
        //   Mono  — 1 byte per pixel (0 = black, non-zero = white), length = width * height
        public static byte[] ToPcx(byte[] imgData, int width, int height, ColorMode mode, byte[] palette = null)
        {
            if (imgData == null || imgData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            // Determine if we aleady have a full PCX file 
            if (imgData.Length >= MinExpectedLen)
            {
                var magic = imgData.ReadBytes(0, Def.MagicBytes.Length);

                if (magic.Matches(Def.MagicBytes))
                {
                    var bpp = imgData[3];
                    var planes = imgData[65];

                    // Idx8 (bpp=8, planes=1) — trailing 256-color palette is required
                    if (bpp == 8 && planes == 1 && !TryReadPalette(imgData, out _))
                        throw new InvalidDataException("Missing or invalid 256-color palette at end of file.");

                    return imgData; // Already a valid PCX file, return as-is
                }

            }

            // Check if imgData contains an embedded 256-color palette;
            // use it if present, otherwise fall back to the palette parameter.
            if ((mode == ColorMode.Detect || mode == ColorMode.Idx8) && TryReadPalette(imgData, out var embeddedPalette))
            {
                palette = embeddedPalette;
                mode = ColorMode.Idx8;

                // Strip the trailing marker (1 byte) + palette (768 bytes) from pixel data
                var pixelLen = imgData.Length - (PaletteLen + 1);
                var pixelData = new byte[pixelLen];
                Buffer.BlockCopy(imgData, 0, pixelData, 0, pixelLen);
                imgData = pixelData;
            }

            // Determine if imgData is raw pixels or RLE-encoded.
            // Raw pixel lengths are unambiguous per mode.
            var pixelCount = width * height;
            var isRawPixels = imgData.Length == pixelCount || imgData.Length == pixelCount * 3;

            // Resolve Detect using the appropriate detection strategy
            if (mode == ColorMode.Detect)
            {
                mode = isRawPixels
                    ? DetectFromPixels(width, height, imgData, palette)
                    : DetectFromRle(width, height, imgData, palette);
            }

            // Validate palette requirements per mode
            switch (mode)
            {
                case ColorMode.Idx8:
                    if (palette == null || palette.Length != PaletteLen)
                        throw new ArgumentException($"Palette '{palette?.Length}' must be exactly {PaletteLen} bytes for {mode}.");
                    break;
                case ColorMode.Ega16:
                    if (palette == null || palette.Length != EgaPaletteLen)
                        throw new ArgumentException($"EGA palette must be exactly {EgaPaletteLen} bytes for {mode}.");
                    break;
            }

            // BytesPerLine (always even) — depends on bits per pixel
            var is8Bit = mode == ColorMode.Rgb24 || mode == ColorMode.Idx8;

            var bytesPerLine = is8Bit
                ? (width + 1) & ~1                    // 8 bpp: 1 byte per pixel
                : (((width + 7) / 8) + 1) & ~1;      // 1 bpp: 1 bit per pixel, rounded up

            int colorPlanes;
            switch (mode)
            {
                case ColorMode.Rgb24: colorPlanes = 3; break;
                case ColorMode.Ega16: colorPlanes = 4; break;
                default: colorPlanes = 1; break; // Idx8, Mono
            }

            byte[] rleBytes;

            if (!isRawPixels)
            {
                // Data is already RLE-encoded with the correct bytesPerLine stride —
                // use it directly to avoid a lossy decode-reencode round trip that
                // would misalign rows when bytesPerLine > width (even-padding).
                rleBytes = imgData;
            }
            else
            {
                // Validate raw pixel data length
                var expectedLen = mode == ColorMode.Rgb24 ? pixelCount * 3 : pixelCount;

                if (imgData.Length != expectedLen)
                    throw new ArgumentException($"Pixels length '{imgData.Length}' does not match expected '{expectedLen}' for {mode}.");

                // Encode scanlines
                var scanLineTotalBytes = bytesPerLine * colorPlanes;
                var scanLineBuffer = new byte[scanLineTotalBytes];
                var rleWriter = new ByteWriter(imgData.Length * 2, DefaultEnc, true);

                for (var y = 0; y < height; y++)
                {
                    Array.Clear(scanLineBuffer, 0, scanLineBuffer.Length);
                    EncodeScanline(imgData, scanLineBuffer, y, width, bytesPerLine, mode);

                    var rle = EncodeRle(scanLineBuffer);
                    rleWriter.Write(rle);
                }

                rleBytes = rleWriter.ToArray();
            }

            // Assemble: header + RLE image data [+ 0x0C marker + 256-color palette for Idx8]
            var egaPalette = mode == ColorMode.Ega16 ? palette : null;
            var header = CreateHeader(width, height, mode, egaPalette);

            var totalSize = header.Length + rleBytes.Length;
            if (mode == ColorMode.Idx8)
                totalSize += 1 + PaletteLen; // 0x0C marker + palette

            var pcx = new ByteWriter(totalSize, DefaultEnc);

            pcx.Write(header);
            pcx.Write(rleBytes);

            switch (mode)
            {
                case ColorMode.Idx8:
                    pcx.Write((byte)PaletteMarker);
                    pcx.Write(palette);
                    break;
            }

            return pcx.ToArray();
        }

        // Encodes one row of raw pixel data into a PCX scanline buffer (planar/packed as needed)
        private static void EncodeScanline(byte[] pixels, byte[] scanLineBuffer, int y, int width, int bytesPerLine, ColorMode mode)
        {
            var rowOffset = y * width;

            switch (mode)
            {
                case ColorMode.Rgb24:
                    // Split interleaved RGB pixels into 3 separate planes (R, G, B)
                    var rgbOffset = rowOffset * 3;
                    for (var x = 0; x < width; x++)
                    {
                        var srcIdx = rgbOffset + x * 3;
                        scanLineBuffer[x] = pixels[srcIdx];       // R plane
                        scanLineBuffer[x + bytesPerLine] = pixels[srcIdx + 1];   // G plane
                        scanLineBuffer[x + bytesPerLine * 2] = pixels[srcIdx + 2];   // B plane
                    }
                    break;

                case ColorMode.Idx8:
                    // Direct copy — 1 palette index per byte
                    Buffer.BlockCopy(pixels, rowOffset, scanLineBuffer, 0, width);
                    break;

                case ColorMode.Ega16:
                    // Pack each 4-bit color index into 4 separate bit planes
                    for (var x = 0; x < width; x++)
                    {
                        var colorIdx = pixels[rowOffset + x] & 0x0F;
                        var byteIdx = x / 8;
                        var bitIdx = 7 - x % 8; // MSB first

                        for (var plane = 0; plane < 4; plane++)
                        {
                            if (((colorIdx >> plane) & 1) == 1)
                                scanLineBuffer[plane * bytesPerLine + byteIdx] |= (byte)(1 << bitIdx);
                        }
                    }
                    break;

                default: // Mono
                    // Pack 1-bit pixels — non-zero = white (bit set)
                    for (var x = 0; x < width; x++)
                    {
                        if (pixels[rowOffset + x] != 0)
                            scanLineBuffer[x / 8] |= (byte)(1 << (7 - x % 8));
                    }
                    break;
            }
        }

        internal static byte[] CreateHeader(int width, int height, ColorMode mode, byte[] egaPalette = null)
        {
            var writer = new ByteWriter(HeaderLen, DefaultEnc);

            // Resolve bitsPerPixel and colorPlanes from ColorMode
            byte bitsPerPixel;
            byte colorPlanes;

            switch (mode)
            {
                case ColorMode.Rgb24:
                    bitsPerPixel = 8;
                    colorPlanes = 3;
                    break;
                case ColorMode.Idx8:
                    bitsPerPixel = 8;
                    colorPlanes = 1;
                    break;
                case ColorMode.Ega16:
                    bitsPerPixel = 1;
                    colorPlanes = 4;
                    break;
                default:
                    bitsPerPixel = 1;
                    colorPlanes = 1;
                    break; // Mono
            }

            var bytesPerLine = bitsPerPixel == 8
                ? (width + 1) & ~1                    // 8 bpp: 1 byte per pixel
                : (((width + 7) / 8) + 1) & ~1;      // 1 bpp: 1 bit per pixel, rounded up

            writer.Write((byte)0x0A);                       // Manufacturer always 0x0A
            writer.Write((byte)0x05);                       // Version 5 = v3.0 or higher
            writer.Write((byte)0x01);                       // PCX run length encoding (Always 1)
            writer.Write(bitsPerPixel);                     // BitsPerPixel / BitsPerPlane

            writer.Write((ushort)0);                        // xStart
            writer.Write((ushort)0);                        // yStart
            writer.Write((ushort)(width - 1));              // xEnd
            writer.Write((ushort)(height - 1));             // yEnd
            writer.Write((ushort)width);                    // HorzRes
            writer.Write((ushort)height);                   // VertRes

            // 16-Color EGA Palette — written for Ega16, zeroed for all other modes
            if (egaPalette != null && egaPalette.Length == EgaPaletteLen)
                writer.Write(egaPalette);
            else
                writer.Skip(EgaPaletteLen);

            writer.Write((byte)0);                          // Reserved1

            writer.Write(colorPlanes);                      // ColorPlanes
            writer.Write((ushort)bytesPerLine);             // BytesPerLine
            writer.Write((ushort)1);                        // PaletteType (always 1 = Color/BW)

            // Remaining header bytes are padding
            writer.Seek(HeaderLen);

            return writer.ToArray();
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Encode / Decode Helpers

        // Parses a PCX header, decodes RLE scanlines, and returns raw BGRA pixel data.
        // Shared by ToBmp and ToPng to avoid duplicating header parsing logic.
        internal static byte[] DecodePcx(byte[] imgData, bool alpha, out int width, out int height)
        {
            if (imgData == null || imgData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            var reader = new ByteReader(imgData);

            var magic = reader.ReadBytes(Def.MagicBytes.Length);

            if (!magic.Matches(Def.MagicBytes))
                throw new NotSupportedException($"Invalid file signature. Expected {Def.MagicBytes.ToHex()}, got {magic.ToHex()}");

            // Peek bpp and realign
            var bpp = reader.PeekByte(3);

            if(Array.IndexOf(SupportedBpp, bpp) == -1)
                throw new NotSupportedException($"Unsupported bits per pixel (BPP) value: {bpp}. Supported values are: {string.Join(", ", SupportedBpp)}.");

            reader.Seek(4);

            // Window dimensions
            var xStart = reader.ReadUInt16();
            var yStart = reader.ReadUInt16();
            var xEnd = reader.ReadUInt16();
            var yEnd = reader.ReadUInt16();

            reader.Skip(4); // horzRes u16 + vertRes u16

            // 16 color palette (48 bytes) (usually ignored in 256-color files)
            var egaPalette = reader.ReadBytes(48);

            reader.Skip(1); // Reserved (Always 0)

            var colorPlanes = reader.ReadByte();
            var bytesPerLine = reader.ReadUInt16();

            reader.Skip(2); // paletteType (u16)

            // Skip remaining header filler to reach byte 128
            reader.Seek(HeaderLen);

            width = xEnd - xStart + 1;
            height = yEnd - yStart + 1;

            if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
                throw new InvalidDataException($"Invalid PCX dimensions: {width}x{height}.");

            // Determine color mode (throws if unsupported)
            var mode = ResolveColorMode(bpp, colorPlanes);

            // Resolve the active palette for the color mode
            byte[] palette;

            switch (mode)
            {
                case ColorMode.Idx8:
                    if (!TryReadPalette(imgData, out palette))
                        throw new InvalidDataException("Missing or invalid 256-color palette at end of file.");
                    break;
                case ColorMode.Ega16:
                    palette = egaPalette;
                    break;
                default:
                    palette = null;
                    break;
            }

            return DecodeScanLines(reader, width, height, bytesPerLine, colorPlanes, mode, palette, alpha);
        }

        // Encodes raw byte data using PCX RLE compression and returns the compressed byte array.
        private static byte[] EncodeRle(byte[] imgData)
        {
            var writer = new ByteWriter(imgData.Length * 2, DefaultEnc, true);

            var pos = 0;
            while (pos < imgData.Length)
            {
                var runCount = 1;
                var runVal = imgData[pos];


                while (pos + runCount < imgData.Length && runCount < 63 && imgData[pos + runCount] == runVal)
                    runCount++;

                if (runCount > 1 || runVal >= 0xC0)
                    writer.Write((byte)(0xC0 | runCount));

                writer.Write(runVal);

                pos += runCount;
            }

            return writer.ToArray();
        }

        // Decodes RLE data into the provided buffer.
        private static void DecodeRle(ByteReader reader, byte[] buffer, int length)
        {
            var pos = 0;

            while (pos < length)
            {
                var b = reader.ReadByte();

                int runCount;
                byte runValue;

                // Check for RLE marker (top 2 bits are 11)
                if ((b & 0xC0) == 0xC0)
                {
                    runCount = b & 0x3F; // Lower 6 bits
                    runValue = reader.ReadByte();
                }
                else
                {
                    runCount = 1;
                    runValue = b;
                }

                // Don't write past the buffer if file is malformed
                var runLimit = Math.Min(runCount, length - pos);

                for (var k = 0; k < runLimit; k++)
                    buffer[pos++] = runValue;
            }

        }

        // Decodes RLE-compressed scanlines and converts pixel data to a BGRA byte array.
        // When alpha is true, palette index 0 (or black for Mono) is treated as fully transparent.
        private static byte[] DecodeScanLines(ByteReader reader, int width, int height,
            int bytesPerLine, int colorPlanes, ColorMode mode, byte[] palette, bool alpha)
        {

            if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
                throw new InvalidDataException($"Invalid PCX dimensions: {width}x{height}.");

            // Minimum bytesPerLine depends on bitsPerPixel and color mode
            var minBytesPerLine = mode == ColorMode.Rgb24 || mode == ColorMode.Idx8
                ? width              // 8 bpp: 1 byte per pixel
                : (width + 7) / 8;  // 1 bpp: 1 bit per pixel, rounded up

            if (bytesPerLine < minBytesPerLine)
                throw new InvalidDataException($"BytesPerLine ({bytesPerLine}) is less than minimum required ({minBytesPerLine}) for width ({width}).");

            // Prepare Output Buffer (BGRA)
            var outImg = new byte[checked(width * height * 4)];

            // Buffer to hold one raw scanline (all planes)
            // PCX lines are padded to be even. 'bytesPerLine' includes this padding.
            var scanLineTotalBytes = bytesPerLine * colorPlanes;
            var scanLineBuffer = new byte[scanLineTotalBytes];

            // Decode Scanlines
            for (var y = 0; y < height; y++)
            {
                // Decompress one scanline of raw data
                DecodeRle(reader, scanLineBuffer, scanLineTotalBytes);

                // Convert Scanline to BGRA
                for (var x = 0; x < width; x++)
                {
                    var outputIndex = (y * width + x) * 4;
                    byte r, g, b, a = 255;

                    switch (mode)
                    {
                        case ColorMode.Rgb24:
                            // 24-bit RGB (3 planes, 8 bits each) — no alpha convention
                            r = scanLineBuffer[x];
                            g = scanLineBuffer[x + bytesPerLine];
                            b = scanLineBuffer[x + bytesPerLine * 2];
                            break;

                        case ColorMode.Idx8:
                            // 8-bit Indexed Color (1 plane, 8 bits) — index 0 = transparent
                            var paletteIndex8 = scanLineBuffer[x];
                            var pIdx8 = paletteIndex8 * 3;
                            r = palette[pIdx8];
                            g = palette[pIdx8 + 1];
                            b = palette[pIdx8 + 2];
                            if (alpha && paletteIndex8 == 0) a = 0;
                            break;

                        case ColorMode.Ega16:
                            // 4-bit (16 colors, 4 planes, 1 bit each) — index 0 = transparent
                            var byteIndex = x / 8;
                            var bitIndex = 7 - x % 8; // MSB first

                            var colorIndex = 0;
                            for (var plane = 0; plane < 4; plane++)
                            {
                                var bit = (scanLineBuffer[plane * bytesPerLine + byteIndex] >> bitIndex) & 1;
                                colorIndex |= bit << plane;
                            }

                            var pIdx4 = colorIndex * 3;
                            r = palette[pIdx4];
                            g = palette[pIdx4 + 1];
                            b = palette[pIdx4 + 2];
                            if (alpha && colorIndex == 0) a = 0;
                            break;

                        default: // Mono
                            // 1-bit Monochrome (1 plane, 1 bit) — black (0) = transparent
                            var monoBit = (scanLineBuffer[x / 8] >> (7 - x % 8)) & 1;
                            r = g = b = monoBit == 1 ? (byte)255 : (byte)0;
                            if (alpha && monoBit == 0) a = 0;
                            break;
                    }

                    outImg[outputIndex + 0] = b;
                    outImg[outputIndex + 1] = g;
                    outImg[outputIndex + 2] = r;
                    outImg[outputIndex + 3] = a;

                } // End of pixel loop

            } // End of scanline loop

            return outImg;

        } // End of DecodeScanlines

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Palette

        // Reads the 256-color RGB palette (768 bytes) after the Rle-compressed image data
        public static bool TryReadPalette(byte[] data, out byte[] palette)
        {
            palette = null;

            // We need at least 769 bytes at the end of the file for the palette marker and palette data
            if (data == null || data.Length < PaletteLen + 1)
                return false;

            // check if the last byte before the palette is the 0x0C marker
            var markerPos = data.Length - (PaletteLen + 1);

            if (data[markerPos] != PaletteMarker)
                return false;

            palette = new byte[PaletteLen];
            Buffer.BlockCopy(data, markerPos + 1, palette, 0, PaletteLen);
            return true;
        }

        // Builds a standard 256-color grayscale palette (768 bytes) where each entry is (i, i, i) for i in [0, 255]
        public static byte[] CreateGrayPalette()
        {
            var pal = new byte[PaletteLen];

            for (var i = 0; i < 256; i++)
            {
                pal[i * 3 + 0] = (byte)i;
                pal[i * 3 + 1] = (byte)i;
                pal[i * 3 + 2] = (byte)i;
            }

            return pal;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Helpers

        private static ColorMode ResolveColorMode(byte bpp, byte planes)
        {
            switch (bpp)
            {
                case 8 when planes == 3:
                    return ColorMode.Rgb24;
                case 8 when planes == 1:
                    return ColorMode.Idx8;
                case 1 when planes == 4:
                    return ColorMode.Ega16;
                case 1 when planes == 1:
                    return ColorMode.Mono;
                default:
                    throw new NotSupportedException($"Unsupported PCX format: BPP={bpp}, Planes={planes}.");
            }
        }

        // Detects color mode from decoded pixel data (used by ToPcx with ColorMode.Detect).
        // Priority: palette length > pixel array length > pixel value range
        private static ColorMode DetectFromPixels(int width, int height, byte[] pixels, byte[] palette)
        {
            // Palette length is the strongest signal
            if (palette != null)
            {
                switch (palette.Length)
                {
                    case PaletteLen:
                        return ColorMode.Idx8;
                    case EgaPaletteLen:
                        return ColorMode.Ega16;
                }
            }

            var pixelCount = width * height;

            // Rgb24 is the only mode with 3 bytes per pixel
            if (pixels.Length == pixelCount * 3)
                return ColorMode.Rgb24;

            if (pixels.Length != pixelCount)
                throw new InvalidDataException(
                    $"Cannot detect color mode: pixel length '{pixels.Length}' doesn't match any expected size for {width}x{height}.");

            // 1 byte per pixel without a palette — scan values to distinguish Mono vs Idx8
            // Ega16 requires a palette so it can't be detected here
            foreach (var t in pixels)
            {
                if (t > 1)
                    return ColorMode.Idx8; // Value > 1 rules out Mono
            }

            return ColorMode.Mono;
        }

        // Detects color mode from raw RLE scanline data by calculating the expected
        // bytesPerLine × planes × height for each mode and checking which fully
        // consumes the RLE input.
        // Ordered by likelihood: Idx8 > Rgb24 > Ega16 > Mono
        public static ColorMode DetectFromRle(int width, int height, byte[] rleData, byte[] palette = null)
        {
            if (rleData == null || rleData.Length == 0)
                throw new ArgumentException("RLE data cannot be null or empty.", nameof(rleData));

            // Palette length is the strongest signal
            if (palette != null)
            {
                switch (palette.Length)
                {
                    case PaletteLen:
                        return ColorMode.Idx8;
                    case EgaPaletteLen:
                        return ColorMode.Ega16;
                }
            }

            // Calculate bytesPerLine for each bpp (always even)
            var bpl8 = (width + 1) & ~1;                       // 8 bpp modes
            var bpl1 = (((width + 7) / 8) + 1) & ~1;           // 1 bpp modes

            // The correct mode's expected size will fully consume the RLE input
            if (TryRleMatchesExpected(rleData, bpl8 * 1 * height)) return ColorMode.Idx8;
            if (TryRleMatchesExpected(rleData, bpl8 * 3 * height)) return ColorMode.Rgb24;
            if (TryRleMatchesExpected(rleData, bpl1 * 4 * height)) return ColorMode.Ega16;
            if (TryRleMatchesExpected(rleData, bpl1 * 1 * height)) return ColorMode.Mono;

            throw new InvalidDataException(
                $"Cannot detect color mode from RLE data ({rleData.Length} bytes) for {width}x{height}.");
        }

        // Returns true if decoding rleData with the given expected length consumes all RLE input exactly.
        private static bool TryRleMatchesExpected(byte[] rleData, int expectedLen)
        {
            if (expectedLen <= 0)
                return false;

            try
            {
                var reader = new ByteReader(rleData);
                var buffer = new byte[expectedLen];
                DecodeRle(reader, buffer, expectedLen);
                return reader.Remaining == 0;
            }
            catch
            {
                // Reader exhausted before filling buffer
                return false;
            }
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

            var bpp = data[3];
            if (Array.IndexOf(SupportedBpp, bpp) == -1)
                return false;

            // Dimensions: xEnd >= xStart, yEnd >= yStart
            var xStart = data.PeekUInt16(4);
            var yStart = data.PeekUInt16(6);
            var xEnd = data.PeekUInt16(8);
            var yEnd = data.PeekUInt16(10);

            return xEnd >= xStart && yEnd >= yStart;
        }
        #endregion

    }

}