
namespace NHQTools.FileFormats.Pff
{
    ////////////////////////////////////////////////////////////////////////////////////
    // CRC-32 calculation methods used for NovaLogic PFF2, PFF3, and PFF4 files
    // ====================================================================
    // CRC-32 used by NovaLogic's PFF2/3/4
    // Polynomial: 0x04C11DB7
    // Init: 0xFFFFFFFF
    // Update: crc = (crc << 8) ^ table[(crc >> 24) ^ byte]
    // Final: crc ^ 0xFFFFFFFF
    // Encode key (Used with /ENCODE flag in PFF4): 0x01E17FCEu
    // ====================================================================
    // sub_401060 in pack-pff3-36.c
    // sub_4010C0 in pack-pff4-36.c
    // ====================================================================
    ////////////////////////////////////////////////////////////////////////////////////
    public class PffCrc
    {
        // Private
        private const uint Polynomial = 0x04C11DB7u;
        private const uint InitValue = 0xFFFFFFFFu;
        private const uint FinalXor = 0xFFFFFFFFu;

        private static readonly uint[] Table = BuildTable();

        ////////////////////////////////////////////////////////////////////////////////////
        #region Pff2 Entry Table
        public static uint ComputePff2EntryTable(byte[] buffer, int offset, int length, int chunkSize = 0x20000) =>
            ComputePff2_3(buffer, offset, length, chunkSize);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region PFF2 and PFF3
        public static uint ComputePff2_3(byte[] buffer, int offset, int length, int chunkSize = 0x20000)
        {
            var crc = InitValue;
            var table = Table; // Cache static field

            var currentOffset = offset;
            var remaining = length;

            while (remaining > 0)
            {
                var currentChunkSize = remaining < chunkSize ? remaining : chunkSize;
                var end = currentOffset + currentChunkSize;

                var i = currentOffset;
                var end4 = end - 3;

                while (i < end4)
                {
                    crc = (crc << 8) ^ table[(crc >> 24) ^ buffer[i]];
                    crc = (crc << 8) ^ table[(crc >> 24) ^ buffer[i + 1]];
                    crc = (crc << 8) ^ table[(crc >> 24) ^ buffer[i + 2]];
                    crc = (crc << 8) ^ table[(crc >> 24) ^ buffer[i + 3]];
                    i += 4;
                }

                while (i < end)
                {
                    crc = (crc << 8) ^ table[(crc >> 24) ^ buffer[i]];
                    i++;
                }

                currentOffset += currentChunkSize;
                remaining -= currentChunkSize;
            }

            return crc ^ FinalXor;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region PFF4
        public static uint ComputePff4(byte[] buffer, int offset, int length, int chunkSize = 0x20000)
        {
            var crc = InitValue;
            var table = Table; // Cache static field

            var startOffset = 0;
            var remaining = length;

            while (remaining > 0)
            {
                var currentChunkSize = remaining < chunkSize ? remaining : chunkSize;
                var chunkStart = offset + startOffset;

                ////////////////////////////////////////////////////////////////////////////////////
                // The original NovaLogic implementation appears to have a bug in it. It may have
                // been intentional. It took many hours off my life to finally understand what
                // was wrong with it, I hope whoever made it steps on a lego.
                // ------------------------------------------------------------
                // sub_4010C0 in pack-pff4-36.c
                // ------------------------------------------------------------
                // It loops (Length / 4) times [(length >> 2)].
                // It advances the ptr (*v2++) and then (v2 - 1) for the remaining 3 updates
                // Which means it uses the same input byte for all 4 CRC updates in that iteration
                // After that, it handles the remaining (Length & 3) bytes normally
                // Decompiled C code of the flawed implementation:
                // v5 = dword_41DBA0[*v2++ ^ (result >> 24)] ^ (result << 8);
                // dword_41DFA0 = v5;
                // v6 = dword_41DBA0[*(v2 - 1) ^ (v5 >> 24)] ^ (v5 << 8);
                // dword_41DFA0 = v6;
                // v7 = dword_41DBA0[*(v2 - 1) ^ (v6 >> 24)] ^ (v6 << 8);
                // dword_41DFA0 = v7;
                // result = dword_41DBA0[*(v2 - 1) ^ (v7 >> 24)] ^ (v7 << 8);
                // ------------------------------------------------------------
                // So we only CRC 1/4th of the data in the main loop
                // ------------------------------------------------------------
                ////////////////////////////////////////////////////////////////////////////////////
                var len = currentChunkSize >> 2; // a2 >> 2

                // crc >> 24 is already 0-255 for a uint; XOR with byte stays 0-255, so & 0xFFu is redundant
                for (var i = 0; i < len; i++)
                {
                    // Reads the byte at the current pointer + 1 byte (*v2++)
                    // This is the only byte that is used per loop iteration
                    var b = buffer[chunkStart + i];

                    // 1
                    crc = (crc << 8) ^ table[(crc >> 24) ^ b];

                    // 2 (same byte)
                    crc = (crc << 8) ^ table[(crc >> 24) ^ b];

                    // 3 (same byte)
                    crc = (crc << 8) ^ table[(crc >> 24) ^ b];

                    // 4 (same byte)
                    crc = (crc << 8) ^ table[(crc >> 24) ^ b];
                }

                // Handles (Length & 3) bytes normally
                // The C code continues using *v2++.
                // Since the main loop advanced v2 by len bytes,
                // continue reading from chunkStart + len.
                var remainder = currentChunkSize & 3;

                for (var j = 0; j < remainder; j++)
                {
                    var b = buffer[chunkStart + len + j];
                    crc = (crc << 8) ^ table[(crc >> 24) ^ b];
                }

                startOffset += currentChunkSize;
                remaining -= currentChunkSize;
            }

            return crc ^ FinalXor;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        private static uint[] BuildTable()
        {
            var table = new uint[256];

            for (uint bv = 0; bv < 256; bv++)
            {
                var reg = bv << 24;

                for (var bi = 0; bi < 8; bi++)
                {

                    if ((reg & 0x80000000u) != 0)
                        reg = (reg << 1) ^ Polynomial;
                    else
                        reg <<= 1;

                }

                table[bv] = reg;

            }

            return table;
        }

    }

}