using System;
using System.IO;
using System.Text;
using System.Drawing;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class Fnt
    {
        // Public
        public const int HeaderLen = 4;
        public const int MinExpectedLen = HeaderLen + 4; // Signature + at least some metadata

        // Atlas is composed of 256px wide "pages"
        // Height varies (Most=256, DFX=512, DFX2=768)
        private const int AtlasWidth = 256;
        private static readonly int[] AtlasHeights = { 768, 512, 256 };
        private const int MaxAtlasPages = 16; // Max observed is 5 in DFX2, but I haven't tested much

        private static readonly Color BackgroundColor = Color.FromArgb(32, 32, 32);
        private const int BytesPerPixel = 4; // 32-bit BGRA

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Fnt() => Def = Definitions.GetFormatDef(FileType.FNT);

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

            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected {Def.MagicBytes.ToHex()}, got {magic.ToHex()}");

            // Detect embedded atlas at EOF
            var (atlasW, atlasH, pixelOffset) = DetectAtlas(imgData.Length);

            // If wider than a single page, crop to the first 256px page
            var outW = atlasW;

            if (atlasW > AtlasWidth && (atlasW % AtlasWidth) == 0)
                outW = AtlasWidth;

            var pixels = (outW == atlasW)
                ? ExtractPixels(imgData, pixelOffset, atlasW, atlasH)
                : CropPixels(imgData, pixelOffset, atlasW, 0, 0, outW, atlasH);

            Images.AlphaFill(pixels, BytesPerPixel, BackgroundColor);

            return Images.CreateBmp(pixels, outW, atlasH);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Helpers

        // Detects the raw 32bpp atlas stored at EOF by trying various dimensions
        // (width = AtlasWidth * 1..MaxAtlasPages, height from AtlasHeights) and selecting
        // the candidate whose pixel region leaves the smallest metadata prefix.
        // Iteration order (height descending, width ascending). Favors taller atlases
        private static (int Width, int Height, long PixelOffset) DetectAtlas(long fileLength)
        {
            var minMeta = long.MaxValue;
            var match = (Width: 0, Height: 0, Offset: 0L);

            foreach (var h in AtlasHeights)
            {
                for (var page = 1; page <= MaxAtlasPages; page++)
                {
                    var w = AtlasWidth * page;
                    var pixelBytes = (long)w * h * BytesPerPixel;

                    if (fileLength <= pixelBytes)
                        break;

                    var meta = fileLength - pixelBytes;

                    if (meta >= minMeta)
                        continue;

                    minMeta = meta;
                    match = (w, h, meta);
                }
            }

            return match.Width == 0 
                ? throw new InvalidDataException("Could not detect atlas dimensions from known patterns.") 
                : (match.Width, match.Height, match.Offset);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static byte[] ExtractPixels(byte[] src, long pixelOffset, int w, int h)
        {
            var len = (long)w * h * BytesPerPixel;

            if (len > int.MaxValue)
                throw new InvalidDataException("Pixel buffer too large.");

            var dst = new byte[(int)len];
            Buffer.BlockCopy(src, (int)pixelOffset, dst, 0, (int)len);
            return dst;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Stride aware crop for multi-page atlases
        private static byte[] CropPixels(byte[] src, long pixelOffset, int srcW, int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || w <= 0 || h <= 0)
                throw new InvalidDataException("Crop rectangle is invalid.");

            if (x + w > srcW)
                throw new InvalidDataException("Crop rectangle exceeds source bounds.");

            var srcStride = srcW * BytesPerPixel;
            var dstStride = w * BytesPerPixel;

            var total = (long)dstStride * h;

            if (total > int.MaxValue)
                throw new InvalidDataException("Cropped pixel buffer exceeds maximum allowed size.");

            var dst = new byte[(int)total];
            var srcBase = (int)pixelOffset + y * srcStride + x * BytesPerPixel;

            for (var row = 0; row < h; row++)
            {
                var srcOff = srcBase + row * srcStride;
                var dstOff = row * dstStride;
                Buffer.BlockCopy(src, srcOff, dst, dstOff, dstStride);
            }

            return dst;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            return data.StartsWith(Def.MagicBytes);
        }
        #endregion

    }

}