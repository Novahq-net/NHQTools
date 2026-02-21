using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Helpers;

namespace NHQTools.FileFormats.Pff
{
    internal class PffEntryTable
    {
        // PffFile Ref
        public PffFile Pff { get; }
        public Encoding Enc => Pff?.Enc;
        public PffVersion Version => Pff?.Version;

        ////////////////////////////////////////////////////////////////////////////////////
        #region CRC (For Entry Table only, ***NOT individual entries***)

        public uint? CrcRead { get; private set; } 
        public uint? CrcComputed { get; private set; }
        public bool CrcMatches => !CrcRead.HasValue || !CrcComputed.HasValue || CrcRead.Value == CrcComputed.Value;

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Entry Table Accessors
        public byte[] RawBytes { get; private set; } // Raw byte dump of the entire entry table for debugging
        public List<PffEntry> Entries { get; } = new List<PffEntry>(); // Complete list of files contained in the PFF  
        public uint EntryCount => (uint)Entries.Count;
        public uint TotalDataSize => (uint)Entries.Sum(e => e.DataSize);
        public uint TotalDeadSpaceSize => (uint)Entries.Where(e => e.DeadSpace).Sum(e => e.DataSize);

        // Cached distinct filter sets, rebuilt via FileTypeFilterCache()
        public HashSet<FileType> DistinctFileTypes { get; private set; } = new HashSet<FileType>();
        public HashSet<string> DistinctFileTypeExtensions { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Basic Helper Methods (Public)
        public int GetNextId() => EntryCount > 0 ? Entries.Max(e => e.Id) + 1 : 1;
        public void AddEntry(PffEntry entry) => Entries.Add(entry);
        public void RemoveEntry(PffEntry entry) => Entries.Remove(entry);
        public void ClearEntries() => Entries.Clear();
        public PffEntry GetEntry(uint id) => Entries.FirstOrDefault(e => e.Id == id); // Returns null if not found
        public PffEntry GetEntry(string name) => Entries.FirstOrDefault(e => e.FileNameStr.Equals(name, StringComparison.OrdinalIgnoreCase)); // Returns null if not found
        public IEnumerable<PffEntry> LiveEntries() => Entries.Where(e => !e.DeadSpace);
        public IEnumerable<PffEntry> DeadEntries() => Entries.Where(e => e.DeadSpace);

        // Rebuilds the cached distinct FileType/FileTypeExt sets from live entries.
        public void BuildFilterCache()
        {
            var types = new HashSet<FileType>();
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in Entries)
            {
                if (entry.DeadSpace)
                    continue;

                types.Add(entry.FileType);

                if (!string.IsNullOrEmpty(entry.FileTypeExt))
                    extensions.Add(entry.FileTypeExt);
            }

            DistinctFileTypes = types;
            DistinctFileTypeExtensions = extensions;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Data Helper Methods (Public)
        public bool RenameEntry(PffEntry entry, string newFileName)
        {
            if (string.IsNullOrEmpty(newFileName))
                return false;

            var validName = FileNameHelper.ValidFileName(newFileName, new FileNameHelper.ValidationOptions
            {
                EnforceAscii = new FileNameHelper.FileNameRule<bool>(true),
                MaxLength = new FileNameHelper.FileNameRule<int>(PffVersion.FileNameLength)
            });

            if (!validName.IsValid)
                return false;

            

            entry.FileNameBytes = Enc.GetBytes(validName.FileName);
            return true;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Returns a CLONE of the entry data
        public byte[] GetEntryData(PffEntry entry) => (byte[])entry.Data.Clone(); 

        ////////////////////////////////////////////////////////////////////////////////////
        // Replaces the entry data from external source with a clone
        public void SetEntryData(PffEntry entry, byte[] data, uint? timestamp = null)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("File data cannot be empty or null", nameof(data));

            // Clone to avoid external modification
            // Data setter triggers CRC, DataSize, FileType
            entry.Data = (byte[])data.Clone();

            // Forced changes for external data modification
            // Data sets CrcComputed, since it's new data, set CrcRead to match
            entry.CrcRead = entry.CrcComputed;
            entry.Timestamp = timestamp ?? (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            entry.DataOffset = 0;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Marks this entry as deleted by setting the flag and renaming it
        // Will trigger DeadSpace and FileNameBytes to refresh with the new values
        public void SetEntryDeadSpace(PffEntry entry)
        {
            // Crc stays the same because it's computed only on the data
            //
            // Don't check for PffFeatures.DeadSpaceFlags before setting DeadSpaceFlags |= 1 here.
            // We want this to trigger a refresh on DeadSpaceFlags, which will in turn refresh DeadSpace
            //
            // It does not matter if PFF0/PFF2 have this flag or not
            entry.DeadSpaceFlags |= 1;
            entry.FileNameBytes = Version.DeadSpaceBytes;
        }

        public void SetEntryDeadSpace(uint id) => SetEntryDeadSpace(GetEntry(id));
        public void SetEntryDeadSpace(string fileName) => SetEntryDeadSpace(GetEntry(fileName));

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region PffEntryTable Constructor / Read / Write / WriteEntryRecord
        public PffEntryTable(PffFile pff)
        {
            Pff = pff;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal static PffEntryTable Read(BinaryReader reader, PffHeader header, PffFile pff)
        {
            // Reads the PFF Entry Table from a file stream. All entries are read in bulk to memory
            // Sacrifices some memory for speed and simplicity, and easier CRC calculation

            var entryTable = new PffEntryTable(pff);
            var enc = pff.Enc;
            var version = pff.Version;

            // Position to start of entry table obtained from header
            reader.BaseStream.Position = header.EntryTableOffset;

            // Calculate total entry table size
            var entryTableSize = (int)(header.EntryTableCount * header.EntryRecordLength);

            // Validate we have enough stream before reading. We read the entire table at once
            // and process it in memory. This keeps it simple and allows easy CRC calculation
            var streamLength = reader.BaseStream.Length;
            
            if (reader.BaseStream.Position + entryTableSize > streamLength)
                throw new EndOfStreamException("Entry table size exceeds stream length");

            // Read entire entry table to memory for processing
            var entryTableRawBytes = reader.ReadBytes(entryTableSize);

            // Parse each entry record from the raw bytes
            for (var i = 0; i < header.EntryTableCount; i++)
            {

                // Use relative offset as we are reading from the byte array not the stream
                var entryOffset = i * (int)header.EntryRecordLength;
                var entryRawBytes = new byte[header.EntryRecordLength];

                // Copy the entire entry record bytes for this entry
                Array.Copy(entryTableRawBytes, entryOffset, entryRawBytes, 0, (int)header.EntryRecordLength);

                // Store the current offset relative to the start of the entry table
                var entry = new PffEntry(i, pff)
                {
                    EntryTableOffset = header.EntryTableOffset + (uint)entryOffset,
                    EntryTableRawBytes = entryRawBytes
                };

                // Parse entry record from raw bytes using memory stream
                using (var msReader = new BinaryReader(new MemoryStream(entry.EntryTableRawBytes), enc))
                {
                    // Use version specific entry record reader
                    version.EntryRecordReader(msReader, entry);

                    // EntryRecordReader is responsible for reading the first field
                    // and any trailing fields, so we just need to read the remaining fields
                    // The below fields are present in all versions and always start at +4
                    msReader.BaseStream.Position = 4;

                    // Common fields
                    entry.DataOffset = msReader.ReadUInt32();
                    entry.DataSize = msReader.ReadUInt32();
                    entry.Timestamp = msReader.ReadUInt32();
                    entry.FileNameBytes = msReader.ReadBytes(PffVersion.FileNameLength);
                }

                // Don't check for 0 data size here as some entries can have 0 size (LW-Mods.pff)
                // if(entry.DataSize <= 0)
                //    throw new InvalidDataException($"Entry data size cannot be 0: {entry.FileNameStr ?? string.Empty} at {entry.entryOffset}");

                // ulong to avoid overflow issues (this really shouldn't be an issue as it would require a 4GB+ file size, but just to be safe)
                if ((ulong)entry.DataOffset + entry.DataSize > (ulong)streamLength)
                    throw new EndOfStreamException($"Entry data size exceeds stream length: {entry.DataOffset + entry.DataSize} > {streamLength}");

                // Add entry to table
                entryTable.AddEntry(entry);
            }

            // Store and entire entry table in RawBytes
            entryTable.RawBytes = entryTableRawBytes;

            // Read and calculate Entry Table CRC (PFF2 Only)
            // ReSharper disable once InvertIf
            if (version.HasEntryTableCrc)
            {
                // Read the entry table CRC. It is located immediately after the entry table
                // NOTE: The current reader position is correct after reading entryTableRawBytes
                // because we read the entire table in one go, the inners are processed with a
                // separate memory stream so the readers position is still at the end of the table
                entryTable.CrcRead = reader.ReadUInt32();

                // Compute CRC
                entryTable.CrcComputed = version.EntryTableCrcType == EntryTableCrcTypes.Pff2_3
                    ? PffCrc.ComputePff2EntryTable(entryTableRawBytes, 0, entryTableRawBytes.Length)
                    : (uint?)null;
            }

            return entryTable;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        /// Writes the Entry Table to the stream.
        /// Handles buffering for PFF2 and direct streaming for all others
        /// PFF2 has a checksum on the entire entry table at the end of the table
        internal static void Write(BinaryWriter writer, PffEntryTable entryTable, PffVersion version, Encoding enc)
        {

            // File stream write without CRC calculation
            if (!version.HasEntryTableCrc)
            {
                foreach (var entry in entryTable.Entries)
                    WriteEntryRecord(writer, entry, version);

            }
            else
            {

                // Write to a memory stream first so we can compute the CRC
                var entryTableSize = (uint)(entryTable.Entries.Count * version.EntryLength);
                var entryTableBytes = new byte[entryTableSize];

                // Copies tableBytes into a writeable memory stream
                // writeable: true so we can write into the fixed stream
                using (var mStream = new MemoryStream(entryTableBytes, writable: true))
                using (var mWriter = new BinaryWriter(mStream, enc))
                {
                    // Write entry record to the memory stream
                    foreach (var entry in entryTable.Entries)
                        WriteEntryRecord(mWriter, entry, version);

                }

                // Compute CRC for the entire table
                entryTable.CrcComputed = PffCrc.ComputePff2EntryTable(entryTableBytes, 0, entryTableBytes.Length);

                // Write the memory stream buffer to the file stream
                writer.Write(entryTableBytes);

                // Write the computed CRC or 0 if we are bad. It really does not seem to matter
                // If the crc was actually used to verify the files by the game, then this would be bad
                writer.Write(entryTable.CrcComputed ?? 0);
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Write an individual entry record to the stream.
        private static void WriteEntryRecord(BinaryWriter writer, PffEntry entry, PffVersion version)
        {
            // Store start position so our EntryRecordWriter knows
            // The start of its own entry
            var startPos = writer.BaseStream.Position;
            var fileName = entry.FileNameBytes;

            // Skip first 4 bytes as we'll write it after the common fields
            writer.Seek(4, SeekOrigin.Current);

            // Common fields
            writer.Write(entry.DataOffset);
            writer.Write(entry.DataSize);
            writer.Write(entry.Timestamp);
            writer.Write(fileName, 0, PffVersion.FileNameLength + 1); // Always enforce full length + null

            // Use version specific writer for any variable fields
            version.EntryRecordWriter(writer, entry, startPos);

            // Realign stream position for the next entry
            writer.BaseStream.Position = startPos + version.EntryLength;
        }

        #endregion

    }

}