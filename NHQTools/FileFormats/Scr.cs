using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class Scr
    {
        // Public
        public const int HeaderLen = 4;
        public const int MinExpectedLen = HeaderLen + 1;

        // This key is only used for DF1, which has no header and is just the raw encrypted payload.
        public const uint DF1Key = 0x04960552;

        // In order of first match likelihood 
        public static readonly uint[] KnownEncryptionKeys = {
            0x2A5A8EAD, // BHD, JOCA, DFX, DFX2
            0xA55B1EED, // JOCA, DFX, DFX2 (.fx)
            0x01234567, // DF2, LW, TFD, C4
            0xABEEFACE, // JO Demo
        };

        // Regex
        private static readonly Regex RxFindKey = new Regex(
            "^// ?KEY:0x([0-9a-f]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Leave \A to ensure we only strip a key comment at the very beginning of the file
        // So when we change this back to multi-line mode, we don't wonder what happened 
        private static readonly Regex RxStripKey = new Regex(
            @"\A// ?KEY:0x[0-9a-f]+[^\S\r\n]*(?:\r\n|\r|\n)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.GetEncoding(Common.NlCodepage);

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Scr() => Def = Definitions.GetFormatDef(FileType.SCR);

        ////////////////////////////////////////////////////////////////////////////////////
        #region To Text
        public static string ToTxt(FileInfo file, SerializeFormat format, Encoding enc = null, uint? encryptionKey = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : ToTxt(File.ReadAllBytes(file.FullName), format, enc, encryptionKey);
        }

        public static string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc = null, uint? encryptionKey = null)
        {
            enc = enc ?? DefaultEnc;

            if (fileData == null || fileData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            if (!Def.SerializeFormats.Contains(format))
                throw new ArgumentException("Unsupported serialization format.", nameof(format));

            var magic = fileData.ReadBytes(0, Def.MagicBytes.Length);

            // Bypass magic byte check for DF1 since it has no header and is just raw encrypted payload.
            // We didn't realize this at first so we had to add special support for it after the fact
            // We don't want to break the normal flow of the code just for this one special case
            if (encryptionKey == DF1Key)
                magic = Def.MagicBytes; 

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            var key = encryptionKey ?? DetectKey(fileData);

            if (!key.HasValue)
                throw new InvalidDataException("Unable to detect encryption key. Please specify the encryption key to use.");

            var scr = Decrypt(fileData, key.Value);

            var outStr = StripKey(enc.GetString(scr));

            return PrependKey(outStr, key.Value);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region From Text
        public static byte[] FromTxt(FileInfo file, SerializeFormat format, Encoding enc = null, uint? encryptionKey = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : FromTxt(File.ReadAllText(file.FullName, enc), format, enc, encryptionKey);
        }

        public static byte[] FromTxt(string textData, SerializeFormat format, Encoding enc = null, uint? encryptionKey = null)
        {
            enc = enc ?? DefaultEnc;

            if (textData == null || textData.Length < 2)
                throw new InvalidDataException("Data is empty or too short.");

            if (!Def.SerializeFormats.Contains(format))
                throw new ArgumentException("Unsupported serialization format.", nameof(format));

            if (!encryptionKey.HasValue)
            {
                // Check to see if a comment with the key exists
                var keyMatch = FindKey(textData);

                if (!keyMatch.Success)
                    throw new InvalidDataException("No encryption key specified. Please specify the encryption key to use, " +
                                                   "or include a comment '//KEY:0xKEYVALUE' at the beginning of the text file.");

                // Convert the hex string to uint
                encryptionKey = Convert.ToUInt32(keyMatch.Groups[1].Value, 16);

            }

            var cleanScr = StripKey(textData);
            var scr = enc.GetBytes(cleanScr);

            return Encrypt(scr, encryptionKey.Value);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decrypt

        private static byte[] Decrypt(byte[] payloadData, uint key)
        {
            // DF1 has no header, just raw encrypted payload
            var headerLen = HeaderLen;
            if(key == DF1Key)
                headerLen = 0;

            // Payload is all bytes after the header
            var payloadLen = payloadData.Length - headerLen;
            var outBytes = new byte[payloadLen];

            // Strip magic bytes
            Buffer.BlockCopy(payloadData, headerLen, outBytes, 0, payloadLen);

            // Opposite order from encryption (Reverse, then XOR)
            outBytes.ReverseBytes(0, payloadLen);
            XorCipher(outBytes, 0, payloadLen, key);

            return outBytes;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Encrypt

        private static byte[] Encrypt(byte[] textData, uint key)
        {
            // DF1 has no header, just raw encrypted payload
            var headerLen = HeaderLen;
            var magicBytes = Def.MagicBytes;

            if (key == DF1Key)
            {
                headerLen = 0;
                magicBytes = Array.Empty<byte>();
            }

            var outBytes = new byte[headerLen + textData.Length];

            Array.Copy(magicBytes, 0, outBytes, 0, magicBytes.Length);

            Buffer.BlockCopy(textData, 0, outBytes, headerLen, textData.Length);

            // Opposite order from decryption (XOR, then Reverse)
            XorCipher(outBytes, headerLen, textData.Length, key);
            outBytes.ReverseBytes(headerLen, textData.Length);

            return outBytes;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Cipher
        private static void XorCipher(byte[] buffer, int pos, int length, uint key)
        {
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("Data cannot be null or empty.", nameof(buffer));

            if (pos < 0 || length < 0 || (long)pos + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Index out of range");

            var end = pos + length;

            for (var i = pos; i < end; i++)
            {
                var rotL = key.RotateLeft(11);

                // Adds the rotated key to itself, rotates again, then XORs with 1
                // (ulong) cast prevents overflow exceptions
                // (uint) cast discards the upper 32 bits
                key = ((uint)((ulong)key + rotL)).RotateLeft(4) ^ 1;

                buffer[i] ^= (byte)key;
            }

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Detect Key

        private static uint? DetectKey(byte[] payloadData, int peekLen = 32)
        {
            var payloadLen = payloadData.Length - HeaderLen;

            // If the payload is too short, we can't reliably detect the key
            if (payloadLen < 2)
                return null;

            // Only test peekLen bytes from the tail of the payload
            var testLen = Math.Min(peekLen, payloadLen - 1); // 32b Wingpos.def fix

            // Copy just the last testLen bytes and reverse them
            var tailStart = HeaderLen + payloadLen - testLen;
            var reversed = new byte[testLen];

            Buffer.BlockCopy(payloadData, tailStart, reversed, 0, testLen);
            reversed.ReverseBytes(0, testLen);

            foreach (var key in KnownEncryptionKeys)
            {
                // Copy reversed tail and apply XOR with key
                var testBytes = new byte[testLen];

                Buffer.BlockCopy(reversed, 0, testBytes, 0, testLen);
                XorCipher(testBytes, 0, testLen, key);

                // Guaranteed!
                if (testBytes.IsPrintableAscii(testLen))
                    return key;
            }

            return null;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Key Comment Helpers

        private static string PrependKey(string text, uint key)
            => $"// KEY:0x{key:X8}{Environment.NewLine}{text}";

        private static Match FindKey(string text) => RxFindKey.Match(text);

        private static string StripKey(string text) => RxStripKey.Replace(text, "");

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            if (!data.StartsWith(Def.MagicBytes))
                return false;

            // Must have at least 1 byte of payload after header
            return data.Length > HeaderLen;
        }
        #endregion

    }

}