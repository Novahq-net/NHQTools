using System;
using System.IO;
using System.Text;

namespace NHQTools.Extensions
{
    public static class StreamExtensions
    {
        ////////////////////////////////////////////////////////////////////////////////////
        #region Read / Write Strings

        public static string ReadCString(this Stream stream, Encoding enc, int maxLength = int.MaxValue)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength cannot be negative.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("ReadCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 reader for Unicode strings.");

            // Buffer
            var chunkSize = 4096;

            if (maxLength != int.MaxValue)
                chunkSize = Math.Min(chunkSize, Math.Max(1, maxLength));

            var chunk = new byte[chunkSize];

            // Use a MemoryStream to accumulate bytes dynamically
            using (var ms = new MemoryStream(capacity: Math.Min(256, maxLength == int.MaxValue ? 256 : maxLength)))
            {
                var remaining = maxLength;

                while (remaining > 0)
                {
                    // Read current chunk
                    var toRead = Math.Min(chunk.Length, remaining);
                    var bytesRead = stream.Read(chunk, 0, toRead);

                    if (bytesRead <= 0)
                        break; // EOF

                    // Scan ahead for null terminator
                    var zeroIndex = -1;
                    for (var i = 0; i < bytesRead; i++)
                    {
                        if (chunk[i] != 0)
                            continue;

                        zeroIndex = i;
                        break;
                    }

                    if (zeroIndex >= 0)
                    {
                        // Write everything up to zeroIndex
                        if (zeroIndex > 0)
                            ms.Write(chunk, 0, zeroIndex);

                        // Reposition stream if we read extra bytes after zeroIndex
                        // bytesRead = total read
                        // zeroIndex = index of 0
                        // (zeroIndex + 1) = consumed bytes including 0
                        var extra = bytesRead - (zeroIndex + 1);

                        if (extra > 0)
                        {
                            if (!stream.CanSeek)
                                throw new NotSupportedException("Stream must be seekable to use the fast buffered ReadCString. For non-seekable streams, use the byte-by-byte overload.");

                            stream.Position -= extra;
                        }

                        break;
                    }

                    // Append chunk to memory stream and continue
                    ms.Write(chunk, 0, bytesRead);
                    remaining -= bytesRead;
                }

                return enc.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }

        }

        public static string ReadCString(this BinaryReader reader, Encoding enc, int maxLength = int.MaxValue)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader), "Reader cannot be null.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength cannot be negative.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("ReadCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 reader for Unicode strings.");

            using (var ms = new MemoryStream())
            {
                var buffer = new byte[1];
                var length = 0;

                while (length < maxLength)
                {
                    // reader.Read() utilizes the internal buffer for BinaryReader
                    if (reader.Read(buffer, 0, 1) == 0)
                        break; // EOF reached

                    if (buffer[0] == 0)
                        break;

                    ms.WriteByte(buffer[0]);
                    length++;
                }

                return enc.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }

        }

        public static void WriteCString(this Stream stream, string str, Encoding enc)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("WriteCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 writer for Unicode strings.");

            var bytes = enc.GetBytes(str ?? string.Empty);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
        }

        public static void WriteCString(this BinaryWriter writer, string str, Encoding enc)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer), "BinaryWriter cannot be null.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("WriteCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 writer for Unicode strings.");

            // Write(string) adds its own length prefix which we can't have in most cases
            // So convert to bytes and write manually
            var bytes = enc.GetBytes(str);

            if (bytes.Length > 0)
                writer.Write(bytes);

            // null terminator
            writer.Write((byte)0);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Stream Utils
        public static void Align(this Stream stream, int alignment)
        {
            if (alignment <= 0)
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be positive.");

            var remainder = stream.Position % alignment;

            if (remainder == 0)
                return;

            var padding = (int)(alignment - remainder);
            var padZero = new byte[padding]; // Allocated once is better than many WriteByte calls

            stream.Write(padZero, 0, padding);
        }

        #endregion

    }

}