using System;
using System.IO;
using System.Text;
using System.IO.Compression;

// NHQTools Libraries
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class Bfc
    {
        // [BFC1 Signature] [Uncompressed Size] [Zlib Header] [Deflate Stream] [Adler32]

        public const int HeaderLen = 8; // "BFC1" (4) + Uncompressed Size (4)
        public const int MinExpectedLen = HeaderLen + 6; // + Zlib Header (2) + Adler32 (4)

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Bfc() => Def = Definitions.GetFormatDef(FileType.BFC);

        ////////////////////////////////////////////////////////////////////////////////////
        #region Unpack

        public static byte[] Unpack(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : Unpack(File.ReadAllBytes(file.FullName));
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static byte[] Unpack(byte[] fileData)
        {
            if (fileData == null || fileData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            var reader = new ByteReader(fileData, DefaultEnc);

            var magic = reader.ReadBytes(Def.MagicBytes.Length);

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            var expectedSize = reader.ReadInt32();

            // Rest of the file is the Zlib Blob 
            var zlibData = reader.ReadBytes(reader.Remaining);

            return Decompress(zlibData, expectedSize);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Pack

        public static byte[] Pack(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : Pack(File.ReadAllBytes(file.FullName));
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static byte[] Pack(byte[] fileData)
        {
            if (fileData == null)
                throw new ArgumentNullException(nameof(fileData), "Data cannot be null.");

            // Zlib Compression (Header + Deflate + Adler)
            var zlibBlock = Compress(fileData);

            var writer = new ByteWriter(HeaderLen + zlibBlock.Length, DefaultEnc);

            // BFC1 Header
            writer.Write(Def.MagicBytes);
            writer.Write(fileData.Length); // Uncompressed Size

            // Zlib Block
            writer.Write(zlibBlock);

            return writer.ToArray();
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decompress

        private static byte[] Decompress(byte[] zlibData, int expectedSize = -1)
        {
            if (zlibData == null || zlibData.Length < 6) // Min size: Header(2) + Adler(4)
                throw new InvalidDataException("Zlib data is too short.");

            var reader = new ByteReader(zlibData, DefaultEnc);

            var cmf = reader.ReadByte(); // Compression Method
            var flg = reader.ReadByte(); // Flags

            // Check CMF (Lower nibble must be 8 for Deflate)
            if ((cmf & 0x0F) != 8)
                throw new NotSupportedException($"Unknown compression method: {cmf & 0x0F}");

            // Check FDICT (Preset dictionary) - bit 5 of FLG
            if ((flg & 0x20) != 0)
                throw new NotSupportedException("Zlib preset dictionary is not supported.");

            // Check FCHECK (header checksum)
            if ((cmf * 256 + flg) % 31 != 0)
                throw new InvalidDataException("Invalid Zlib header checksum.");

            // Strip Header (2 bytes) and Footer (4 bytes)
            const int headerLen = 2; // CMF + FLG
            const int footerLen = 4; // Adler32

            var payloadLen = zlibData.Length - headerLen - footerLen;

            if (payloadLen < 0)
                throw new InvalidDataException("Zlib payload is empty.");

            using (var inStream = new MemoryStream(zlibData, headerLen, payloadLen))
            using (var deflate = new DeflateStream(inStream, CompressionMode.Decompress))
            using (var outStream = new MemoryStream(expectedSize > 0 ? expectedSize : 4096))
            {
                deflate.CopyTo(outStream);

                // Verify Adler-32 footer (stored big-endian)
                var storedAdler = (uint)(
                    (zlibData[zlibData.Length - 4] << 24) |
                    (zlibData[zlibData.Length - 3] << 16) |
                    (zlibData[zlibData.Length - 2] << 8) |
                     zlibData[zlibData.Length - 1]);

                var result = outStream.ToArray();
                var computedAdler = Adler32(result);

                return storedAdler != computedAdler
                    ? throw new InvalidDataException($"Adler-32 checksum mismatch. Expected 0x{storedAdler:X8}, got 0x{computedAdler:X8}.")
                    : result;
            }

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Compress

        private static byte[] Compress(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                // Write Zlib Header (0x78 0x9C is default compression)
                ms.WriteByte(0x78); // Compression Method
                ms.WriteByte(0x9C); // Flags

                // Leave stream open to continue writing Adler32
                using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                    deflate.Write(data, 0, data.Length);

                // Write Adler-32 Checksum (Big Endian)
                var adler = Adler32(data);

                ms.WriteByte((byte)((adler >> 24) & 0xFF));
                ms.WriteByte((byte)((adler >> 16) & 0xFF));
                ms.WriteByte((byte)((adler >> 8) & 0xFF));
                ms.WriteByte((byte)((adler) & 0xFF));

                return ms.ToArray();
            }

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Adler32
        // Unrolled to process data in blocks vs byte-by-byte
        private static uint Adler32(byte[] data)
        {
            if (data == null)
                return 1;

            const uint b = 65521; // Largest prime smaller than 65536
            const int max = 5552;   // N_MAX is the largest n such that 255n(n+1)/2 + (n+1)(Base-1) <= 2^32-1

            uint s1 = 1;
            uint s2 = 0;
            var len = data.Length;
            var offset = 0;

            while (len > 0)
            {
                var k = len < max ? len : max;
                len -= k;

                while (k >= 16)
                {
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    s1 += data[offset++]; s2 += s1;
                    k -= 16;
                }

                while (k > 0)
                {
                    s1 += data[offset++];
                    s2 += s1;
                    k--;
                }

                s1 %= b;
                s2 %= b;
            }

            return (s2 << 16) | s1;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        /// Calculates the Adler-32 checksum for the specified byte array using a byte-by-byte algorithm.
        /// Adler-32 per zlib (mod 65521) Simple byte-by-byte version for reference/testing
        /// https://en.wikipedia.org/wiki/Adler-32
        private static uint Adler32Ref(byte[] data)
        {
            const uint mod = 65521;
            uint a = 1;
            uint b = 0;

            foreach (var t in data)
            {
                a = (a + t) % mod;
                b = (b + a) % mod;
            }

            return (b << 16) | a;
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

            // Uncompressed size should be non-zero
            var uncompressedSize = data.PeekUInt32(4);
            if (uncompressedSize == 0)
                return false;

            // Zlib header: lower nibble of CMF must be 8 (Deflate)
            var cmf = data[HeaderLen];
            if ((cmf & 0x0F) != 8)
                return false;

            // FCHECK: (CMF * 256 + FLG) % 31 == 0
            var flg = data[HeaderLen + 1];
            return (cmf * 256 + flg) % 31 == 0;
        }
        #endregion
    }

}