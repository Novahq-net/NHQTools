using System;
using System.IO;
using System.Collections.Generic;

namespace NHQTools.FileFormats.Pff
{
    ////////////////////////////////////////////////////////////////////////////////////
    // The first 4 bytes should indicate the length of the header, which is typically 20 bytes.
    // Some PFF editors write non-standard values in this field that do not affect file loading in game
    // If we always read and write 20 bytes for the header, we avoid any possible compatibility issues.
    // The header includes version information, entry table count, record length, and the offset to the entry table.
    // The ReadLength property reflects the value read from the first 4 bytes, which may differ from the standard length
    // The VersionBytes property identifies the PFF version (e.g., "PFF0", "PFF2", "PFF3", "PFF4") "F4" appears in Black Hawk Down
    // PFF files, but they are really PFF3 w/CRC. "F4" only appears in Black Hawk Down and only from the original installation.
    // Using the supplied pack.exe with Black Hawk Down creates a "PFF3" signature.
    ////////////////////////////////////////////////////////////////////////////////////
    internal class PffHeader
    {
        private static readonly byte[] EmptyByte = Array.Empty<byte>();

        // We can't rely on this in the writer since editors such as FWO Raven PFF Editor write non-standard length values even though the length is always 20 bytes
        // When the first 4 bytes of the file is > 20 the extra bytes appear to be the length of the extra bytes after the standard signature in the footer ie: 
        // ", Modified By FwO Raven's Pff Utility v0.7" bytes[2C 20 4D 6F 64 69 66 69 65 64 20 42 79 20 46 77 4F 20 52 61 76 65 6E 27 73 20 50 66 66 20 55 74 69 6C 69 74 79 20 76 30 2E 37]
        // When the header length is 0 the pff file contains a list of files with no data blocks (Tachyon _Files.pff)
        // know values: 0 (Tachyon _Files.pff 2000-07-11 17:32) 20, 62 (FWO Raven PFF Editor ak47.pff)
        public const uint Length = 20;
        public uint ReadLength { get; private set; } // Value read from first 4 bytes.
        public byte[] VersionBytes { get; private set; } //known values: PFF0, PFF2, PFF3, PFF4, F4 <= but PFF3
        public uint EntryTableCount { get; private set; } //includes <DEAD SPACE> entries
        public uint EntryRecordLength { get; private set; } //know values: 32, 36
        public uint EntryTableOffset { get; private set; } //offset to start of entry table from beginning of file

        // Raw bytes of the entire header for debugging and writing.
        // This will always produce a standard 20 byte header
        // regardless of the value in ReadLength or the length of VersionBytes
        public byte[] RawBytes
        {
            get
            {
                if (VersionBytes == null)
                    return EmptyByte;

                var bytes = new List<byte>();
                bytes.AddRange(BitConverter.GetBytes(ReadLength));
                bytes.AddRange(VersionBytes);
                bytes.AddRange(BitConverter.GetBytes(EntryTableCount));
                bytes.AddRange(BitConverter.GetBytes(EntryRecordLength));
                bytes.AddRange(BitConverter.GetBytes(EntryTableOffset));

                return bytes.ToArray();
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal PffHeader() { }

        internal PffHeader(PffVersion version)
        {
            VersionBytes = version.VersionBytes;
            EntryTableCount = 0;
            EntryRecordLength = version.EntryLength;
            EntryTableOffset = 0;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal static PffHeader Read(BinaryReader reader)
        {
            reader.BaseStream.Position = 0;

            return new PffHeader
            {
                ReadLength = reader.ReadUInt32(),
                VersionBytes = reader.ReadBytes(PffVersion.VersionBytesLength),
                EntryTableCount = reader.ReadUInt32(),
                EntryRecordLength = reader.ReadUInt32(),
                EntryTableOffset = reader.ReadUInt32(),
            };

        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal static void Write(BinaryWriter writer, PffHeader header, uint entryTableCount, uint entryTableOffset)
        {
            writer.BaseStream.Position = 0;

            // Update header values to align with current data
            header.EntryTableCount = entryTableCount;
            header.EntryTableOffset = entryTableOffset;

            // Enforce standard length header when writing
            // Some editors write non-standard lengths that have no
            // effect on reading or loading the file in game
            writer.Write(Length);
            writer.Write(header.VersionBytes);
            writer.Write(entryTableCount);
            writer.Write(header.EntryRecordLength);
            writer.Write(entryTableOffset);

        }

    }

}