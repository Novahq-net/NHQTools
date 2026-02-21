using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NHQTools.Utilities
{
    public static class Images
    {
        // Removes the alpha channel from a 32bpp ARGB bitmap by setting all alpha values to fully opaque.
        public static void RemoveAlphaChannel(Bitmap bmp)
        {

            if(bmp == null)
                throw new ArgumentNullException(nameof(bmp), "Bitmap cannot be null.");

            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
                throw new ArgumentException("Bitmap must be 32bpp ARGB.", nameof(bmp));

            // Lock the entire bitmap for write access
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            try
            {
                // Calculate total bytes
                var totalBytes = data.Stride * bmp.Height;
                var pixels = new byte[totalBytes];

                // Copy from native memory to managed array
                Marshal.Copy(data.Scan0, pixels, 0, totalBytes);

                // Iterate every 4th byte (Blue, Green, Red, [Alpha]) and set to 255
                for (var i = 3; i < totalBytes; i += 4)
                    pixels[i] = 255;

                // Copy back to native memory
                Marshal.Copy(pixels, 0, data.Scan0, totalBytes);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Alpha fill BGRA pixel data against a solid background color.
        public static void AlphaFill(byte[] bgraPixels, int bytesPerPixel, Color background)
        {
            var bgB = background.B;
            var bgG = background.G;
            var bgR = background.R;

            for (var i = 0; i < bgraPixels.Length; i += bytesPerPixel)
            {
                var alpha = bgraPixels[i + 3];

                if (alpha == 255)
                    continue; // Fully opaque

                if (alpha == 0)
                {
                    // Fully transparent
                    bgraPixels[i + 0] = bgB;
                    bgraPixels[i + 1] = bgG;
                    bgraPixels[i + 2] = bgR;
                    bgraPixels[i + 3] = 255;
                    continue;
                }

                // Partial transparency
                var inv = 255 - alpha;
                bgraPixels[i + 0] = (byte)((bgraPixels[i + 0] * alpha + bgB * inv) / 255);
                bgraPixels[i + 1] = (byte)((bgraPixels[i + 1] * alpha + bgG * inv) / 255);
                bgraPixels[i + 2] = (byte)((bgraPixels[i + 2] * alpha + bgR * inv) / 255);
                bgraPixels[i + 3] = 255;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static Bitmap CreateBmp(byte[] imgData, int width, int height, PixelFormat pixelFormat = PixelFormat.Format32bppArgb, RotateFlipType rotateFlip = RotateFlipType.RotateNoneFlipNone)
        {
            if (width < 0 || height < 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be non-negative.");

            // Ceiling division handles sub-byte formats (1bpp, 4bpp) without truncating to 0
            var bitsPerPixel = Image.GetPixelFormatSize(pixelFormat);
            var bytesPerRow = (width * bitsPerPixel + 7) / 8;
            var expectedBytes = bytesPerRow * height;

            if (imgData == null || imgData.Length < expectedBytes)
                throw new ArgumentException("Image data is too short for the specified dimensions.", nameof(imgData));

            var bmp = new Bitmap(width, height, pixelFormat);

            try
            {
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    pixelFormat
                );

                try
                {

                    var scan0 = bmpData.Scan0.ToInt64();

                    for (var y = 0; y < height; y++)
                    {
                        var dest = new IntPtr(scan0 + y * bmpData.Stride);
                        Marshal.Copy(imgData, y * bytesPerRow, dest, bytesPerRow);
                    }

                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                if (rotateFlip != RotateFlipType.RotateNoneFlipNone)
                    bmp.RotateFlip(rotateFlip);

                return bmp;
            }
            catch
            {
                bmp.Dispose();
                throw;
            }

        }

        ///////////////////////////////////////////////////////////////////////////////////
        #region CreateType / CreatePng / CreateJpg / CreateGif
        public static byte[] CreateType(byte[] imgData, int width, int height, ImageFormat imgFormat, PixelFormat pixelFormat = PixelFormat.Format32bppArgb, RotateFlipType rotateFlip = RotateFlipType.RotateNoneFlipNone)
        {
            using (var bmp = CreateBmp(imgData, width, height, pixelFormat, rotateFlip))
            using (var stream = new MemoryStream())
            {
                bmp.Save(stream, imgFormat);
                return stream.ToArray();
            }

        }

        ///////////////////////////////////////////////////////////////////////////////////
        public static byte[] CreatePng(byte[] imgData, int width, int height, PixelFormat pixelFormat = PixelFormat.Format32bppArgb, RotateFlipType rotateFlip = RotateFlipType.RotateNoneFlipNone)
            => CreateType(imgData, width, height, ImageFormat.Png, pixelFormat, rotateFlip);

        ///////////////////////////////////////////////////////////////////////////////////
        public static byte[] CreateJpg(byte[] imgData, int width, int height, PixelFormat pixelFormat = PixelFormat.Format32bppArgb, RotateFlipType rotateFlip = RotateFlipType.RotateNoneFlipNone)
            => CreateType(imgData, width, height, ImageFormat.Jpeg, pixelFormat, rotateFlip);

        ///////////////////////////////////////////////////////////////////////////////////
        public static byte[] CreateGif(byte[] imgData, int width, int height, PixelFormat pixelFormat = PixelFormat.Format32bppArgb, RotateFlipType rotateFlip = RotateFlipType.RotateNoneFlipNone)
            => CreateType(imgData, width, height, ImageFormat.Gif, pixelFormat, rotateFlip);
        #endregion

    }

}
