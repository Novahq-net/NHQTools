using System;
using System.Text;

namespace NHQTools.Extensions
{
    public class ByteReader
    {
        // Public
        public Encoding Enc => _enc;
        public int Length => _data.Length;
        public int Remaining => _data.Length - _pos;
        public byte[] Data => _data;
        public int Position
        {
            get => _pos;
            set
            {
                if (value < 0 || value > _data.Length)
                    throw new ArgumentOutOfRangeException(nameof(value), "Position is out of range.");

                _pos = value;
            }
        }

        // Private
        private readonly Encoding _enc;
        private readonly byte[] _data;
        private int _pos;

        ////////////////////////////////////////////////////////////////////////////////////
        public ByteReader(byte[] data, Encoding enc = null)
        {
            _enc = enc ?? Encoding.ASCII;
            _data = data ?? throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            _pos = 0;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Primitives

        public short ReadInt16() => _data.ReadInt16Le(ref _pos);
        public ushort ReadUInt16() => _data.ReadUInt16Le(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public int ReadInt32() => _data.ReadInt32Le(ref _pos);
        public uint ReadUInt32() => _data.ReadUInt32Le(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public long ReadInt64() => _data.ReadInt64Le(ref _pos);
        public ulong ReadUInt64() => _data.ReadUInt64Le(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public float ReadFloat() => _data.ReadFloat(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public double ReadDouble() => _data.ReadDouble(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public sbyte ReadSByte() => _data.ReadSByte(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public byte ReadByte() => _data.ReadByte(ref _pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public byte[] ReadBytes(int length) => _data.ReadBytes(ref _pos, length);

        public void ReadBytes(byte[] buffer, int offset, int count) => _data.ReadBytes(ref _pos, buffer, offset, count);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Strings
        public string ReadString(int count) => _data.ReadString(ref _pos, count, _enc);

        public string ReadString(int count, Encoding enc) => _data.ReadString(ref _pos, count, enc); // Alt encoding

        public string ReadCString(int maxLength = int.MaxValue) => _data.ReadCString(ref _pos, Enc, maxLength);

        public string ReadCString(Encoding enc, int maxLength = int.MaxValue) => _data.ReadCString(ref _pos, enc, maxLength); // Alt encoding

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Peeks without position change
        public byte PeekByte(int pos) => _data.ReadByte(pos);
        public sbyte PeekSByte(int pos) => _data.ReadSByte(pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public short PeekInt16(int pos) => _data.ReadInt16Le(pos);
        public ushort PeekUInt16(int pos) => _data.ReadUInt16Le(pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public int PeekInt32(int pos) => _data.ReadInt32Le(pos);
        public uint PeekUInt32(int pos) => _data.ReadUInt32Le(pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public long PeekInt64(int pos) => _data.ReadInt64Le(pos);
        public ulong PeekUInt64(int pos) => _data.ReadUInt64Le(pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public float PeekFloat(int pos) => _data.ReadFloat(pos);
        public double PeekDouble(int pos) => _data.ReadDouble(pos);

        ////////////////////////////////////////////////////////////////////////////////////
        public byte[] PeekBytes(int pos, int length) => _data.ReadBytes(pos, length);

        ////////////////////////////////////////////////////////////////////////////////////
        public string PeekString(int pos, int maxLength = int.MaxValue) => _data.ReadCString(pos, _enc, maxLength);

        public string[] PeekStrings(int pos, int count, int maxLength = int.MaxValue)
        {
            var strings = new string[count];

            for (var i = 0; i < count; i++)
            {
                strings[i] = _data.ReadCString(pos, _enc, maxLength);
                pos += strings[i].Length + 1; // Move past the null terminator
            }

            return strings;
        }

        // Peek with alternate encoding
        public string PeekString(int pos, Encoding altEnc, int maxLength = int.MaxValue) => _data.ReadCString(pos, altEnc, maxLength);

        public string[] PeekStrings(int pos, int count, Encoding altEnc, int maxLength = int.MaxValue)
        {
            var strings = new string[count];

            for (var i = 0; i < count; i++)
            {
                strings[i] = _data.ReadCString(pos, altEnc, maxLength);
                pos += strings[i].Length + 1; // Move past the null terminator
            }

            return strings;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Navigation / Positioning

        public void Rewind(int length) => _data.Rewind(ref _pos, length);
        public void Skip(int length) => _data.Skip(ref _pos, length);
        public void Skip(uint length) => _data.Skip(ref _pos, length);
        public void Seek(int pos) => _data.Seek(ref _pos, pos);
        public void Seek(uint pos) => _data.Seek(ref _pos, pos);

        #endregion

    }

}