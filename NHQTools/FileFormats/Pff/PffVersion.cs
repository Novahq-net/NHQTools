using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NHQTools.FileFormats.Pff
{
    [Flags]
    public enum PffFeatures
    {
        None = 0,
        EntryCrc = 1,
        EntryTableCrc = 2,
        DeadSpaceFlags = 4,
        FooterIpAddress = 8,
        FooterZeroPadding = 16,
        FooterSignature = 32
    }

    ////////////////////////////////////////////////////////////////////////////////////
    public enum EntryTableCrcTypes
    {
        None,
        Pff2_3,
        Pff4
    }

    ////////////////////////////////////////////////////////////////////////////////////
    public enum EntryCrcTypes
    {
        None,
        Pff2_3,
        Pff4
    }

    ////////////////////////////////////////////////////////////////////////////////////
    public class PffVersion
    {

        ////////////////////////////////////////////////////////////////////////////////////
        // Always 15 characters max for file name, 15 + null terminator
        // DO NOT CHANGE THIS TO 16
        public const int FileNameLength = 15;
        public const int VersionBytesLength = 4;
        public const string DefaultDeadSpaceStr = "<DEAD SPACE>";

        ////////////////////////////////////////////////////////////////////////////////////
        // Always 20 bytes for header but read notes in PffHeader.cs
        public uint HeaderLength { get; private set; } = 20;
        public byte[] VersionBytes { get; private set; }
        public string VersionStr { get; private set; }
        public uint? VersionInt { get; private set; }
        public string VersionStrExt => VersionStr + "-" + EntryLength;
        public bool VersionSigIsF4ButItsReallyPff3 { get; private set; }
        public static byte[] VersionSigIsF4ButItsReallyPff3Bytes => new byte[] { 0x01, 0x00, 0x46, 0x34 };
        public uint EntryLength { get; private set; }
        internal byte[] DeadSpaceBytes { get; private set; }
        public string DeadSpaceStr { get; private set; }
        public PffFeatures Features { get; private set; } = PffFeatures.None;
        public EntryCrcTypes EntryCrcType { get; private set; } = EntryCrcTypes.None;
        public EntryTableCrcTypes EntryTableCrcType { get; private set; } = EntryTableCrcTypes.None;
        public int FooterLength { get; private set; }

        ////////////////////////////////////////////////////////////////////////////////////
        // Read/Write actions
        internal Action<BinaryReader, PffEntry> EntryRecordReader { get; private set; }
        internal Action<BinaryWriter, PffEntry, long> EntryRecordWriter { get; private set; }

        ////////////////////////////////////////////////////////////////////////////////////
        // Helpers
        public bool HasEntryTableCrc => (Features & PffFeatures.EntryTableCrc) != 0;
        public bool HasFooterIpAddress => (Features & PffFeatures.FooterIpAddress) != 0;
        public bool HasFooterZeroPadding => (Features & PffFeatures.FooterZeroPadding) != 0;
        public bool HasFooterSignature => (Features & PffFeatures.FooterSignature) != 0;

        ////////////////////////////////////////////////////////////////////////////////////
        #region Version Detection
        internal static PffVersion FromGame(PffGameInfo game, Encoding enc)
        {
            var versionBytes = new[]
            {
                (byte)'P', 
                (byte)'F', 
                (byte)'F',
                (byte)('0' + game.VersionNumber) // convert int 3 into char '3'
            };

            return FromSignature(versionBytes, game.EntryRecordLength, enc);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal static PffVersion FromHeader(PffHeader header, Encoding enc) 
            => FromSignature(header.VersionBytes, header.EntryRecordLength, enc);

        ////////////////////////////////////////////////////////////////////////////////////
        private static PffVersion FromSignature(byte[] versionBytes, uint entryLength, Encoding enc)
        {

            // Version signature must be exactly 4 bytes
            if (versionBytes == null || versionBytes.Length != 4)
                throw new NotSupportedException("Version signature does not match the expected bytes");

            // Handle special case where version signature indicates 'F4' but is actually PFF3
            var swappedVersionBytes = false;
            if (versionBytes.SequenceEqual(VersionSigIsF4ButItsReallyPff3Bytes))
            {
                swappedVersionBytes = true;
                versionBytes = new[] { (byte)'P', (byte)'F', (byte)'F', (byte)'3' };
            }

            // Initialize version instance
            var version = new PffVersion
            {
                VersionBytes = versionBytes,
                VersionStr = enc.GetString(versionBytes),
                EntryLength = entryLength,
                HeaderLength = 20, // Assume this is always 20 even though it isn't. Does not impact anything....
                VersionInt = ParseVersionNumber(versionBytes),
                VersionSigIsF4ButItsReallyPff3 = swappedVersionBytes,
            };

            if (version.VersionInt == null)
                throw new InvalidDataException("Unable to determine version number from supplied bytes: " + enc.GetString(versionBytes));

            switch (version.VersionInt.Value)
            {
                case 0:
                    if (!Pff0(version, entryLength))
                        throw new NotSupportedException("PFF0 is supported, but no config exists for an entry length of " + entryLength);
                    break;

                case 2:
                    if (!Pff2(version, entryLength))
                        throw new NotSupportedException("PFF2 is supported, but no config exists for an entry length of " + entryLength);
                    break;

                case 3:
                case 4:
                    if (!Pff3_4(version, entryLength))
                        throw new NotSupportedException("PFF"+ version.VersionInt.Value + " is supported, but no config exists for an entry length of " + entryLength);
                    break;

                default:
                    throw new NotSupportedException("Unsupported / unknown PFF format");
            }

            var deadSpaceBytes = enc.GetBytes(DefaultDeadSpaceStr);

            version.DeadSpaceBytes = deadSpaceBytes;
            version.DeadSpaceStr = enc.GetString(deadSpaceBytes);

            return version;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static uint? ParseVersionNumber(byte[] bytes)
        {
            var last = bytes[3]; //Can't be null 

            if (last == (byte)'0' || last == (byte)'2' || last == (byte)'3' || last == (byte)'4')
                return (uint)(last - '0'); // ASCII digit

            return null;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Read / Write Actions
        private static bool Pff0(PffVersion version, uint entryLength)
        {
            if (entryLength != 32)
                return false;

            version.FooterLength = 0;

            version.EntryRecordReader = (msReader, entry) =>
            {
                msReader.BaseStream.Position = 0;

                // reads 4 bytes to keep the position aligned
                // don't actually store these bytes
                _ = msReader.ReadUInt32(); 
            };

            version.EntryRecordWriter = (fsWriter, entry, startPos) =>
            {
                fsWriter.BaseStream.Position = startPos;
                fsWriter.Write((uint)0); // ZeroPad first 4 bytes
            };

            return true;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static bool Pff2(PffVersion version, uint entryLength)
        {
            if (entryLength != 32)
                return false;

            version.FooterLength = 0;

            version.EntryCrcType = EntryCrcTypes.Pff2_3;
            version.EntryTableCrcType = EntryTableCrcTypes.Pff2_3;
   
            version.Features = PffFeatures.EntryCrc | 
                               PffFeatures.EntryTableCrc;

            version.EntryRecordReader = (msReader, entry) =>
            {
                msReader.BaseStream.Position = 0;
                entry.CrcRead = msReader.ReadUInt32();
            };

            // Using ms for clarity since this is different from all the rest
            // We are writing to the supplied memory stream which is used because we 
            // need to calculate the CRC after writing the entire entry table
            version.EntryRecordWriter = (msWriter, entry, startPos) =>
            {
                msWriter.BaseStream.Position = startPos;
                msWriter.Write(entry.CrcComputed ?? 0);
            };

            return true;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static bool Pff3_4(PffVersion version, uint entryLength)
        {
            version.FooterLength = 12;

            version.Features = PffFeatures.DeadSpaceFlags |
                               PffFeatures.FooterIpAddress |
                               PffFeatures.FooterZeroPadding |
                               PffFeatures.FooterSignature;

            switch (entryLength)
            {
                case 32:

                    version.EntryRecordReader = (msReader, entry) =>
                    {
                        msReader.BaseStream.Position = 0;
                        entry.DeadSpaceFlags = msReader.ReadUInt32();
                    };

                    version.EntryRecordWriter = (fsWriter, entry, startPos) =>
                    {
                        fsWriter.BaseStream.Position = startPos;
                        fsWriter.Write(entry.DeadSpaceFlags);
                    };

                    return true;
                case 36:

                    version.Features |= PffFeatures.EntryCrc; // Append Entry CRC feature

                    version.EntryCrcType = version.VersionInt == 4 && !version.VersionSigIsF4ButItsReallyPff3 
                        ? EntryCrcTypes.Pff4 
                        : EntryCrcTypes.Pff2_3;

                    version.EntryRecordReader = (msReader, entry) =>
                    {
                        msReader.BaseStream.Position = 0;
                        entry.DeadSpaceFlags = msReader.ReadUInt32();

                        msReader.BaseStream.Position = 32;
                        entry.CrcRead = msReader.ReadUInt32();
                    };

                    version.EntryRecordWriter = (fsWriter, entry, startPos) =>
                    {
                        fsWriter.BaseStream.Position = startPos;
                        fsWriter.Write(entry.DeadSpaceFlags);

                        fsWriter.BaseStream.Position = startPos + 32;
                        fsWriter.Write(entry.CrcComputed ?? 0);
                    };

                    return true;
                default:
                    return false;
            }
        }

        #endregion


    }

}