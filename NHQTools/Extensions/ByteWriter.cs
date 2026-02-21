using System;
using System.Text;

namespace NHQTools.Extensions
{
    public class ByteWriter
    {
        // Public
        public Encoding Enc => _enc;
        public int Length => _data.Length;
        public int Remaining => _data.Length - _pos;

        // *** WARNING ***
        // This exposes the internal buffer directly when dynamicResize is enabled.
        // Data may become stale if write triggers a resize
        // Use ToArray() to get a copy of the currently written data when dynamic resizing is enabled.
        public byte[] Data => _data; // Raw buffer might be larger than actual written data
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
        public int Capacity => _data.Length;
        public int Written => _pos;

        // Private
        private readonly Encoding _enc;
        private byte[] _data;
        private int _pos;

        private readonly bool _dynamicResize;
   
        // Delegates
        private delegate void WriteGenericDelegate<in T>(byte[] data, ref int pos, T value);

        ////////////////////////////////////////////////////////////////////////////////////
        public ByteWriter(byte[] data, Encoding enc)
        {
            _enc = enc ?? Encoding.ASCII;
            _data = data ?? throw new ArgumentNullException(nameof(data), "Data cannot be null.");
            _pos = 0;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public ByteWriter(int capacity, Encoding enc, bool dynamicResize = false)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            _enc = enc ?? Encoding.ASCII;
            _data = new byte[capacity];
            _pos = 0;
            _dynamicResize = dynamicResize;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Delegate-Based Write Methods
        private void WriteGeneric<T>(T value, int size, WriteGenericDelegate<T> action)
        {
            EnsureCapacity(size);
            action(_data, ref _pos, value);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Helpers
        private void EnsureCapacity(int additionalBytes)
        {
            if (_pos + additionalBytes <= _data.Length)
                return;

            if (!_dynamicResize)
                throw new InvalidOperationException($"Cannot write {additionalBytes} bytes to fixed-size buffer. (Size: {_data.Length}, Pos: {_pos})");

            // Double the size, or fit exactly if doubling isn't sufficient
            var newSize = Math.Max((long)_data.Length * 2, (long)_pos + additionalBytes);

            // Check for overflow and maximum array length
            if (newSize > int.MaxValue)
                throw new OutOfMemoryException($"Required buffer size ({newSize}) exceeds maximum array length.");

            // Allocates a new array and copies from the current one
            Array.Resize(ref _data, (int)newSize);
        }

        public byte[] ToArray()
        {
            var result = new byte[_pos];
            Buffer.BlockCopy(_data, 0, result, 0, _pos);
            return result;
        }
        #endregion

        /////////////////////////////////////////////////////////////////////////////////////
        #region Primitives
        public void Write(short value) => WriteGeneric(value, 2, ByteExtensions.WriteInt16Le);
        public void Write(ushort value) => WriteGeneric(value, 2, ByteExtensions.WriteUInt16Le);

        ////////////////////////////////////////////////////////////////////////////////////
        public void Write(int value) => WriteGeneric(value, 4, ByteExtensions.WriteInt32Le);
        public void Write(uint value) => WriteGeneric(value, 4, ByteExtensions.WriteUInt32Le);

        ////////////////////////////////////////////////////////////////////////////////////
        public void Write(long value) => WriteGeneric(value, 8, ByteExtensions.WriteInt64Le);
        public void Write(ulong value) => WriteGeneric(value, 8, ByteExtensions.WriteUInt64Le);

        ////////////////////////////////////////////////////////////////////////////////////
        public void Write(float value) => WriteGeneric(value, 4, ByteExtensions.WriteFloat);
        public void Write(double value) => WriteGeneric(value, 8, ByteExtensions.WriteDouble);

        ////////////////////////////////////////////////////////////////////////////////////
        public void Write(sbyte value) => WriteGeneric(value, 1, ByteExtensions.WriteSByte);

        ////////////////////////////////////////////////////////////////////////////////////
        public void Write(byte value) => WriteGeneric(value, 1, ByteExtensions.WriteByte);
        public void Write(byte[] value)
        {
            if (value == null || value.Length == 0)
                return;

            WriteGeneric(value, value.Length, ByteExtensions.WriteBytes);
        }
        public void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null || count == 0)
                return;

            EnsureCapacity(count);
            _data.WriteBytes(ref _pos, buffer, offset, count);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Strings

        public void WriteString(string value) => WriteString(value, _enc);
        public void WriteString(string value, Encoding enc)
        {
            if (string.IsNullOrEmpty(value))
                return;

            var size = enc.GetByteCount(value);
            EnsureCapacity(size);
            _data.WriteString(ref _pos, value, enc);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public void WriteCString(string value) => WriteCString(value, _enc);
        public void WriteCString(string value, Encoding enc)
        {
            var str = value ?? string.Empty;
            var size = enc.GetByteCount(str) + 1; // +1 for null terminator
            EnsureCapacity(size);
            _data.WriteCString(ref _pos, str, enc);
        }

        #endregion

        /////////////////////////////////////////////////////////////////////////////////////
        #region Navigation / Positioning
        public void Rewind(int count) => _data.Rewind(ref _pos, count);
        public void Skip(uint count) => _data.Skip(ref _pos, count);
        public void Seek(int offset) => _data.Seek(ref _pos, offset);
        public void Seek(uint offset) => _data.Seek(ref _pos, offset);

        #endregion

    }

}