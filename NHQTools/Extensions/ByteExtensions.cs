using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace NHQTools.Extensions
{
    public static class ByteExtensions
    {
        private static readonly byte[] EmptyByte = Array.Empty<byte>();  

        ////////////////////////////////////////////////////////////////////////////////////
        #region Peeks
        public static byte PeekByte(this byte[] data, int pos) => data.ReadByte(pos);
        public static sbyte PeekSByte(this byte[] data, int pos) => data.ReadSByte(pos);

        public static short PeekInt16(this byte[] data, int pos) => data.ReadInt16Le(pos);
        public static ushort PeekUInt16(this byte[] data, int pos) => (ushort)data.ReadInt16Le(pos);

        public static int PeekInt32(this byte[] data, int pos) => data.ReadInt32Le(pos);
        public static uint PeekUInt32(this byte[] data, int pos) => (uint)data.ReadInt32Le(pos);

        public static long PeekInt64(this byte[] data, int pos) => data.ReadInt64Le(pos);
        public static ulong PeekUInt64(this byte[] data, int pos) => (ulong)data.ReadInt64Le(pos);

        public static float PeekFloat(this byte[] data, int pos) => data.ReadFloat(pos);
        public static double PeekDouble(this byte[] data, int pos) => data.ReadDouble(pos);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Int16 Little Endian

        /// <summary>
        /// Reads a 16-bit signed integer from a byte array (Little Endian).
        /// </summary>
        public static short ReadInt16Le(this byte[] data, int pos)
        {
            if (pos < 0 || (long)pos + 2 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            return (short)(data[pos] | (data[pos + 1] << 8));
        }
        public static ushort ReadUInt16Le(this byte[] data, int pos) => (ushort)data.ReadInt16Le(pos);

        public static short ReadInt16Le(this byte[] data, ref int refPos)
        {
            // Bounds check in ReadInt16Le

            var val = data.ReadInt16Le(refPos);
            refPos += 2;
            return val;
        }
        public static ushort ReadUInt16Le(this byte[] data, ref int refPos) => (ushort)data.ReadInt16Le(ref refPos);

        public static void WriteInt16Le(this byte[] data, int pos, short value)
        {

            if (pos < 0 || (long)pos + 2 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            data[pos] = (byte)value;
            data[pos + 1] = (byte)(value >> 8);
        }
        public static void WriteUInt16Le(this byte[] data, int pos, ushort value) => data.WriteInt16Le(pos, (short)value);

        public static void WriteInt16Le(this byte[] data, ref int refPos, short value)
        {
            // Bounds check in WriteInt16Le

            data.WriteInt16Le(refPos, value);
            refPos += 2;
        }
        public static void WriteUInt16Le(this byte[] data, ref int refPos, ushort value)
        {
            // Bounds check in WriteUInt16Le

            data.WriteUInt16Le(refPos, value);
            refPos += 2;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Int32 Little Endian

        public static int ReadInt32Le(this byte[] data, int pos)
        {

            if (pos < 0 || (long)pos + 4 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            return data[pos]
                   | (data[pos + 1] << 8)
                   | (data[pos + 2] << 16)
                   | (data[pos + 3] << 24);
        }
        public static uint ReadUInt32Le(this byte[] data, int pos) => (uint)data.ReadInt32Le(pos);

        public static int ReadInt32Le(this byte[] data, ref int refPos)
        {
            var val = data.ReadInt32Le(refPos);
            refPos += 4;
            return val;
        }
        public static uint ReadUInt32Le(this byte[] data, ref int refPos) => (uint)data.ReadInt32Le(ref refPos);

        public static void WriteInt32Le(this byte[] data, int pos, int value)
        {

            if (pos < 0 || (long)pos + 4 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            data[pos] = (byte)value;
            data[pos + 1] = (byte)(value >> 8);
            data[pos + 2] = (byte)(value >> 16);
            data[pos + 3] = (byte)(value >> 24);
        }
        public static void WriteUInt32Le(this byte[] data, int pos, uint value) => data.WriteInt32Le(pos, (int)value);

        public static void WriteInt32Le(this byte[] data, ref int refPos, int value)
        {
            // Bounds check in WriteInt32Le

            data.WriteInt32Le(refPos, value);
            refPos += 4;
        }
        public static void WriteUInt32Le(this byte[] data, ref int refPos, uint value) => data.WriteInt32Le(ref refPos, (int)value);

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Int64 Little Endian

        public static long ReadInt64Le(this byte[] data, int pos)
        {

            if (pos < 0 || (long)pos + 8 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            var lo = (uint)(data[pos] | (data[pos + 1] << 8) |
                            (data[pos + 2] << 16) | (data[pos + 3] << 24));
            var hi = (uint)(data[pos + 4] | (data[pos + 5] << 8) |
                            (data[pos + 6] << 16) | (data[pos + 7] << 24));

            return (long)(((ulong)hi << 32) | lo);
        }
        public static ulong ReadUInt64Le(this byte[] data, int pos) => (ulong)data.ReadInt64Le(pos);

        public static long ReadInt64Le(this byte[] data, ref int refPos)
        {
            // Bounds check in ReadInt64Le

            var val = data.ReadInt64Le(refPos);
            refPos += 8;
            return val;
        }
        public static ulong ReadUInt64Le(this byte[] data, ref int refPos) => (ulong)data.ReadInt64Le(ref refPos);

        public static void WriteInt64Le(this byte[] data, int pos, long value)
        {

            if (pos < 0 || (long)pos + 8 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            data[pos] = (byte)value;
            data[pos + 1] = (byte)(value >> 8);
            data[pos + 2] = (byte)(value >> 16);
            data[pos + 3] = (byte)(value >> 24);
            data[pos + 4] = (byte)(value >> 32);
            data[pos + 5] = (byte)(value >> 40);
            data[pos + 6] = (byte)(value >> 48);
            data[pos + 7] = (byte)(value >> 56);
        }
        public static void WriteUInt64Le(this byte[] data, int pos, ulong value) => data.WriteInt64Le(pos, (long)value);

        public static void WriteInt64Le(this byte[] data, ref int refPos, long value)
        {
            // Bounds check in WriteInt64Le

            data.WriteInt64Le(refPos, value);
            refPos += 8;
        }
        public static void WriteUInt64Le(this byte[] data, ref int refPos, ulong value) => data.WriteInt64Le(ref refPos, (long)value);

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Float

        public static float ReadFloat(this byte[] data, int pos)
        {
            // Bounds check in ReadInt32Le
            // Endianness is handled by ReadInt32Le/WriteInt32Le.
            return BitConverter.ToSingle(BitConverter.GetBytes(data.ReadInt32Le(pos)), 0);
        }

        public static float ReadFloat(this byte[] data, ref int refPos)
        {
            // Bounds check in ReadFloat

            var v = data.ReadFloat(refPos);
            refPos += 4;
            return v;
        }

        public static void WriteFloat(this byte[] data, int pos, float value)
        {
            // Bounds check in WriteInt32Le
            // Endianness is handled by ReadInt32Le/WriteInt32Le.
            data.WriteInt32Le(pos, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }

        public static void WriteFloat(this byte[] data, ref int refPos, float value)
        {
            // Bounds check in WriteFloat

            data.WriteFloat(refPos, value);
            refPos += 4;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Double

        public static double ReadDouble(this byte[] data, int pos)
        {
            // Bounds check in ReadInt64Le

            var v = data.ReadInt64Le(pos);   // little-endian bits
            return BitConverter.Int64BitsToDouble(v);
        }

        public static double ReadDouble(this byte[] data, ref int refPos)
        {
            // Bounds check in ReadDouble

            var v = data.ReadDouble(refPos);
            refPos += 8;
            return v;
        }

        public static void WriteDouble(this byte[] data, int pos, double value)
        {
            // Bounds check in WriteInt64Le

            var val = BitConverter.DoubleToInt64Bits(value);
            data.WriteInt64Le(pos, val);
        }

        public static void WriteDouble(this byte[] data, ref int refPos, double value)
        {
            // Bounds check in WriteDouble

            data.WriteDouble(refPos, value);
            refPos += 8;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region SByte

        public static sbyte ReadSByte(this byte[] data, int pos)
        {
            if (pos < 0 || (long)pos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            return (sbyte)data[pos];
        }

        public static sbyte ReadSByte(this byte[] data, ref int refPos)
        {
            // Bounds check in ReadByte

            var result = data.ReadByte(refPos);
            refPos += 1;
            return (sbyte)result;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void WriteSByte(this byte[] data, int pos, sbyte value)
        {
            if (pos < 0 || (long)pos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            data[pos] = (byte)value;
        }

        public static void WriteSByte(this byte[] data, ref int refPos, sbyte value)
        {
            // Bounds check in WriteSByte

            data.WriteSByte(refPos, value);
            refPos += 1;
        }



        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Byte (single)
        public static byte ReadByte(this byte[] data, int pos)
        {
            if (pos < 0 || (long)pos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            return data[pos];
        }

        public static byte ReadByte(this byte[] data, ref int refPos)
        {
            // Bounds check in ReadByte

            var result = data.ReadByte(refPos);
            refPos += 1;
            return result;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void WriteByte(this byte[] data, int pos, byte value)
        {
            if (pos < 0 || (long)pos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            data[pos] = value;
        }

        public static void WriteByte(this byte[] data, ref int refPos, byte value)
        {
            // Bounds check in WriteByte
            data.WriteByte(refPos, value);
            refPos += 1;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Bytes

        public static byte[] ReadBytes(this byte[] data, int pos, int length)
        {

            if (pos < 0 || length < 0 || (long)pos + length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            var result = new byte[length];
            Buffer.BlockCopy(data, pos, result, 0, length);
            return result;
        }

        public static byte[] ReadBytes(this byte[] data, ref int refPos, int length)
        {
            // Bounds check in ReadBytes

            var result = data.ReadBytes(refPos, length);
            refPos += length;
            return result;
        }

        public static void ReadBytes(this byte[] data, ref int refPos, byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer), "Buffer cannot be null.");

            if (refPos < 0 || count < 0 || (long)refPos + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(refPos), "Position is out of range.");

            if (offset < 0 || (long)offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range.");

            Buffer.BlockCopy(data, refPos, buffer, offset, count);
            refPos += count;
        }

        ////////////////////////////////////////////////////////////////////////////////////

        public static void WriteBytes(this byte[] data, int pos, byte[] value)
        {
            if (value == null || value.Length == 0)
                return;

            if (pos < 0 || (long)pos + value.Length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            Buffer.BlockCopy(value, 0, data, pos, value.Length);
        }

        public static void WriteBytes(this byte[] data, ref int refPos, byte[] value)
        {
            if (value == null || value.Length == 0)
                return;

            data.WriteBytes(refPos, value);
            refPos += value.Length;
        }

        public static void WriteBytes(this byte[] data, ref int refPos, byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer), "Buffer cannot be null.");

            if (refPos < 0 || count < 0 || (long)refPos + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(refPos), "Position is out of range.");

            if (offset < 0 || (long)offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range.");

            Buffer.BlockCopy(buffer, offset, data, refPos, count);
            refPos += count;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region String

        public static string ReadString(this byte[] data, int pos, int count, Encoding enc)
        {
            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (pos < 0 || count < 0 || (long)pos + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            return enc.GetString(data, pos, count);
        }

        public static string ReadString(this byte[] data, ref int refPos, int count, Encoding enc)
        {
            // Bounds check in ReadString

            var val = data.ReadString(refPos, count, enc);
            refPos += count;
            return val;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void WriteString(this byte[] data, int pos, string value, Encoding enc)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Value cannot be null.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            var bytes = enc.GetBytes(value);

            if (pos < 0 || (long)pos + bytes.Length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);
        }

        public static void WriteString(this byte[] data, ref int refPos, string value, Encoding enc)
        {
            // Bounds check and write handled in WriteString
            data.WriteString(refPos, value, enc);
            refPos += enc.GetByteCount(value);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region String (C String)

        public static string ReadCString(this byte[] data, int pos, Encoding enc, int maxLength = int.MaxValue)
        {
            if (pos < 0 || pos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength cannot be negative.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("ReadCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 reader for Unicode strings.");

            // Ensure we don't read past the end of the buffer
            var length = Math.Min(data.Length - pos, maxLength);
            var nullIdx = Array.IndexOf(data, (byte)0, pos, length);

            if (nullIdx < 0)
                nullIdx = pos + length; // Read up to max length if no null found

            return enc.GetString(data, pos, nullIdx - pos);
        }

        public static string ReadCString(this byte[] data, ref int refPos, Encoding enc, int maxLength = int.MaxValue)
        {
            if (refPos < 0 || refPos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(refPos), "Position is out of range.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "MaxLength cannot be negative.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("ReadCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 reader for Unicode strings.");

            // Ensure we don't read past the end of the buffer
            var length = Math.Min(data.Length - refPos, maxLength);
            var nullIdx = Array.IndexOf(data, (byte)0, refPos, length);

            if (nullIdx < 0)
            {
                var result = enc.GetString(data, refPos, length);
                refPos += length;
                return result;
            }

            var str = enc.GetString(data, refPos, nullIdx - refPos);
            refPos = nullIdx + 1;
            return str;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void WriteCString(this byte[] data, int pos, string str, Encoding enc)
        {
            if (pos < 0 || (long)pos >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            if (enc == null)
                throw new ArgumentNullException(nameof(enc), "Encoding cannot be null.");

            if (enc.Equals(Encoding.Unicode) || enc.Equals(Encoding.BigEndianUnicode) || enc.Equals(Encoding.UTF32))
                throw new NotSupportedException("WriteCString assumes a single 0x00 terminator (ASCII/UTF-8). Use a wchar/UTF-16 writer for Unicode strings.");

            var bytes = enc.GetBytes(str ?? string.Empty);

            if ((long)pos + bytes.Length + 1 > data.Length)
                throw new ArgumentException("String is too long for the buffer at the current position.");

            Buffer.BlockCopy(bytes, 0, data, pos, bytes.Length);

            data[pos + bytes.Length] = 0; // Null Terminator
        }

        public static void WriteCString(this byte[] data, ref int refPos, string str, Encoding enc)
        {
            // Bounds check and write handled in WriteCString
            data.WriteCString(refPos, str, enc);
            refPos += enc.GetByteCount(str ?? string.Empty) + 1;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Matches
        public static bool Matches(this byte[] data, byte[] pattern)
        {
            if (ReferenceEquals(data, pattern))
                return true;

            if (data == null || pattern == null || data.Length != pattern.Length)
                return false;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] != pattern[i])
                    return false;
            }

            return true;
        }

        public static bool Matches(this byte[] data, byte[] pattern, int pos)
        {
            if (data == null || pattern == null)
                return false;

            if (pos < 0 || (long)pos + pattern.Length > data.Length)
                return false;

            for (var i = 0; i < pattern.Length; i++)
            {
                if (data[pos + i] != pattern[i])
                    return false;
            }

            return true;
        }

        public static bool Matches(this byte[] dataA, int posA, byte[] dataB, int posB, int length)
        {
            if (dataA == null || dataB == null)
                return false;

            if (posA < 0 || (long)posA + length > dataA.Length)
                return false;

            if (posB < 0 || (long)posB + length > dataB.Length)
                return false;

            for (var i = 0; i < length; i++)
            {
                if (dataA[posA + i] != dataB[posB + i])
                    return false;
            }

            return true;
        }

        public static bool StartsWith(this byte[] data, byte[] pattern)
        {

            if (data == null || pattern == null)
                return false;

            if (data.Length < pattern.Length)
                return false;

            for (var i = 0; i < pattern.Length; i++)
                if (data[i] != pattern[i])
                    return false;

            return true;
        }

        public static bool IsPrintableAscii(this byte[] data, int length = int.MaxValue, bool charsOnly = false)
        {
            var limit = Math.Min(length, data.Length);

            for (var i = 0; i < limit; i++)
            {
                var b = data[i];

                // Printable ASCII
                if (b >= 0x20 && b <= 0x7E) 
                    continue;

                // Allow [tab, LF, CR] if charsOnly is false (text files, etc.)
                if (!charsOnly && (b == 0x09 || b == 0x0A || b == 0x0D))
                    continue; 

                return false;
            }
            return true;
        }

        public static List<int> FindMatches(this byte[] data, byte[] pattern, int pos, int length)
        {
            var matches = new List<int>();

            if (data == null || pattern == null || data.Length == 0 || pattern.Length == 0)
                return matches;

            var start = Math.Max(0, pos);
            var end = Math.Min(length, data.Length);

            while (start <= end - pattern.Length)
            {
                var index = data.IndexOf(pattern, start);

                if (index < 0 || index >= end)
                    break;

                matches.Add(index);
                start = index + 1; // allow overlaps
            }

            return matches;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Padding ***Truncate WARNING***
        public static byte[] RPad(this byte[] bytes, int length, bool nullTerminator = true)
        {
            if (bytes == null)
                bytes = EmptyByte;

            var maxLen = nullTerminator ? length - 1 : length;

            if (bytes.Length > maxLen)
                throw new ArgumentException($"Data length ({bytes.Length}) exceeds fixed field size ({length}).", nameof(bytes));

            var buffer = new byte[length];

            Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);

            return buffer;
        }

        // **WARNING**: This method does not check if the source array exceeds the specified length and will truncate data if necessary.
        public static byte[] RPadTruncate(this byte[] bytes, int length, bool nullTerminator = true)
        {
            if (bytes == null)
                bytes = EmptyByte;

            var buffer = new byte[length];

            var maxLen = nullTerminator ? length - 1 : length;
            var outBytes = Math.Min(bytes.Length, maxLen);

            Buffer.BlockCopy(bytes, 0, buffer, 0, outBytes);

            return buffer;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Bit Operations

        public static uint RotateLeft(this uint value, int offset)
        {
            offset &= 0x1F; // Limit to 0-31
            return (value << offset) | (value >> (32 - offset));
        }

        public static uint RotateRight(this uint value, int offset)
        {
            offset &= 0x1F; // Limit to 0-31
            return (value >> offset) | (value << (32 - offset));
        }

        public static void ReverseBytes(this byte[] buffer, int pos, int length)
        {
            Array.Reverse(buffer, pos, length);
        }

        public static bool IsSet(this byte b, int bitIndex)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "BitIndex is out of range.");

            // (1 << bitIndex) creates a mask like 00001000
            // & checks if that specific bit is set
            return (b & (1 << bitIndex)) != 0;
        }

        public static byte SetBit(this byte b, int bitIndex, bool value)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "BitIndex is out of range.");

            if (value)
                return (byte)(b | (1 << bitIndex)); // Turn ON (OR)

            return (byte)(b & ~(1 << bitIndex)); // Turn OFF (AND NOT)
        }

        public static byte ToggleBit(this byte b, int bitIndex)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "BitIndex is out of range.");

            return (byte)(b ^ (1 << bitIndex)); // XOR toggles
        }

        public static ushort Swap(this ushort value)
        {
            // 0xAABB > 0xBBAA
            return (ushort)((value >> 8) | (value << 8));
        }

        public static uint Swap(this uint value)
        {
            // 0xAABBCCDD > 0xDDCCBBAA
            return ((value & 0x000000FF) << 24) |
                   ((value & 0x0000FF00) << 8) |
                   ((value & 0x00FF0000) >> 8) |
                   ((value & 0xFF000000) >> 24);
        }

        public static byte[] Slice(this byte[] data, int pos, int count)
        {

            if (pos < 0 || count < 0 || (long)pos + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            var outBytes = new byte[count];

            Buffer.BlockCopy(data, pos, outBytes, 0, count);

            return outBytes;
        }

        public static ArraySegment<byte> Segment(this byte[] data, int pos, int count)
        {

            if (pos < 0 || count < 0 || (long)pos + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Position is out of range.");

            // segment.Array, segment.Offset, segment.Count
            return new ArraySegment<byte>(data, 1, 3);
        }

        public static int IndexOf(this byte[] data, byte[] pattern, int pos = 0)
        {
            if (data == null || pattern == null || data.Length == 0 || pattern.Length == 0 || pattern.Length > data.Length)
                return -1;

            if (pos < 0)
                pos = 0;

            // Pre-calculate end limit
            var limit = data.Length - pattern.Length;

            for (var i = pos; i <= limit; i++)
            {
                // Check if pattern matches at this position
                var match = true;

                for (var j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] == pattern[j])
                        continue;

                    match = false;
                    break;
                }

                if (match) 
                    return i;

            }

            return -1;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Debugging / Hex

        public static string ToHex(this byte[] bytes, string separator = "-", int maxBytes = int.MaxValue)
        {

            //string.Join(" ", data.Select(b => $"0x{b:X2}"))
            //BitConverter.ToString(data).Replace("-", " 0x")

            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            // Use built-in BitConverter for the common case of no separator and no truncation
            if (separator == "-" && maxBytes >= bytes.Length)
                return BitConverter.ToString(bytes);

            var limit = Math.Min(bytes.Length, maxBytes);
            var sb = new StringBuilder(limit * 3);

            for (var i = 0; i < limit; i++)
            {
                if (i > 0)
                    sb.Append(separator);

                sb.Append(bytes[i].ToString("X2"));
            }

            if (bytes.Length > limit)
                sb.Append("...");

            return sb.ToString();
        }

        public static string ToHex(this IEnumerable<byte> bytes, string separator = "-", int maxBytes = int.MaxValue)
        {
            return bytes == null 
                ? string.Empty 
                : string.Join(separator, bytes.Take(maxBytes).Select(b => $"{b:X2}"));
        }

        public static string HexDump(this byte[] data, int startPos, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0) 
                return string.Empty;

            var sb = new StringBuilder();
            var len = data.Length;

            for (var i = 0; i < len; i += bytesPerLine)
            {
                var lineWidth = Math.Min(bytesPerLine, len - i);

                sb.AppendFormat("{0:X8}: ", startPos + i);

                // Hex bytes
                for (var j = 0; j < bytesPerLine; j++)
                {
                    if (j < lineWidth)
                        sb.Append(data[i + j].ToString("X2") + " ");
                    else
                        sb.Append("   ");
                }

                sb.Append(" ");

                // ASCII representation
                for (var j = 0; j < lineWidth; j++)
                {
                    var b = data[i + j];
                    var c = (b >= 32 && b <= 126) ? (char)b : '.';

                    sb.Append(c);
                }

                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        public static string AsString(this byte[] data, Encoding enc = null)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            return (enc ?? Encoding.ASCII).GetString(data);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Navigation / Positioning

        public static void Rewind(this byte[] data, ref int refPos, int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");

            if ((long)refPos - length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Position is out of range.");

            refPos -= length;
        }

        public static void Skip(this byte[] data, ref int refPos, int length)
        {
            if (length < 0 || (long)refPos + length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "Position is out of range.");

            refPos += length;
        }

        public static void Skip(this byte[] data, ref int refPos, uint length)
        {
            if (refPos + length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "Position is out of range.");

            refPos += (int)length;
        }

        public static void Seek(this byte[] data, ref int refPos, int pos)
        {
            if (pos < 0 || pos > data.Length) // allows pos == data.Length
                throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of range {data.Length}.");

            refPos = pos;
        }

        public static void Seek(this byte[] data, ref int refPos, uint pos)
        {
            if (pos > data.Length) // allows pos == data.Length
                throw new ArgumentOutOfRangeException(nameof(pos), $"Position {pos} is out of range {data.Length}.");

            refPos = (int)pos;
        }

        #endregion


    }

}