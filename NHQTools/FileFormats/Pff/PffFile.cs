using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Helpers;
using NHQTools.Utilities;
using NHQTools.Exceptions;
using NHQTools.Extensions;

namespace NHQTools.FileFormats.Pff
{
    ////////////////////////////////////////////////////////////////////////////////////
    // PffFile is the public factory for creating, reading and writing PFF archive files
    //
    // Usage:
    //   var pff = PffFile.FromFile("game.pff");       // Open existing PFF
    //   var pff = PffFile.Create(PffGameInfo game);   // Create new for game
    //   pff.Save(@"C:\outout\File.pff");              // Save to disk
    //
    // Querying entries:
    //   var entry = pff.GetEntry("filename.ext");      // By name
    //   var entry = pff.GetEntry(3);                   // By id
    //   foreach (var e in pff.Entries) { ... }         // Iteration
    //
    // Mutating entries:
    //   pff.GetEntryData(entry);                     // Entry data as clone of byte[]
    //   pff.SetEntryData(entry, newBytes[]);         // Replace data with clone of byte[]
    //   pff.RenameEntry(entry, newName);             // Rename entry
    //   pff.SetEntryDeadSpace(entry);                // Mark as deleted
    //
    // Import / Export:
    //   pff.ImportEntry(new FileInfo("new.dat"));      // Import file from disk
    //   pff.ExportEntry(@"C:\output", entry);          // Export to disk
    //   
    ////////////////////////////////////////////////////////////////////////////////////
    public class PffFile
    {
        // Single source of truth for encoding and version
        // *** ALL internal classes derive from this ***
        public Encoding Enc { get; }
        public FileInfo SourceFile { get; private set; }
        internal FileInfo DestFile { get; private set; }

        // PFF file structure
        internal PffHeader Header { get; set; }
        internal PffEntryTable EntryTable { get; set; }
        internal PffFooter Footer { get; set; }

        // Keep this public for easy access to version-specific info like game,
        // max entries, etc. without having to go through header or footer
        public PffVersion Version { get; internal set; }

        // Read only enumerable collection for iteration, LINQ and data binding
        public IEnumerable<PffEntry> Entries => EntryTable?.Entries ?? Enumerable.Empty<PffEntry>();

        ////////////////////////////////////////////////////////////////////////////////////
        #region  Read only Properties

        public string VersionStr => Version?.VersionStr;
        public uint? VersionNumber => Version?.VersionInt;
        public string VersionStrExt => Version?.VersionStrExt;
        public byte[] HeaderRawBytes => Header?.RawBytes;
        public byte[] FooterRawBytes => Footer?.RawBytes;
        public string FooterSignature => Footer?.GetSignatureStr(Enc);
        public string FooterSignatureExtended => Footer?.GetSignatureExtendedStr(Enc);

        // Status
        public uint EntryCount => EntryTable?.EntryCount ?? 0;
        public uint TotalDataSize => EntryTable.TotalDataSize;
        public uint TotalDeadSpaceSize => EntryTable.TotalDeadSpaceSize;

        // Crc
        public bool EntryTableCrcMatches => EntryTable?.CrcMatches ?? true;
        public uint? EntryTableCrcRead => EntryTable?.CrcRead;
        public uint? EntryTableCrcComputed => EntryTable?.CrcComputed;

        // File Types
        public HashSet<FileType> DistinctFileTypes => EntryTable?.DistinctFileTypes;
        public HashSet<string> DistinctFileTypeExtensions => EntryTable?.DistinctFileTypeExtensions;
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Get Entry
   
        public PffEntry GetEntry(string name) => EntryTable?.GetEntry(name);

        public PffEntry GetEntry(uint id) => EntryTable?.GetEntry(id);

        // Returns a clone of the entry data
        public byte[] GetEntryData(PffEntry entry) => EntryTable.GetEntryData(entry);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Set / Rename Entry

        // Replaces an entry data with a clone
        public void SetEntryData(PffEntry entry, byte[] fileBytes) => EntryTable.SetEntryData(entry, fileBytes);

        public bool RenameEntry(PffEntry entry, string newFileName) => EntryTable.RenameEntry(entry, newFileName);

        public void SetEntryDeadSpace(PffEntry entry) => EntryTable.SetEntryDeadSpace(entry);

        public void RebuildFilterCache() => EntryTable.BuildFilterCache();
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Constructors
        ////////////////////////////////////////////////////////////////////////////////////
        private PffFile(Encoding enc = null)
        {
            Enc = enc ?? Encoding.GetEncoding(Common.NlCodepage);
            SourceFile = null;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private PffFile(FileInfo file, Encoding enc = null)
        {
            Enc = enc ?? Encoding.GetEncoding(Common.NlCodepage);
            SourceFile = file;
        }
        #endregion 

        ////////////////////////////////////////////////////////////////////////////////////
        #region Open / Create / Save
        ////////////////////////////////////////////////////////////////////////////////////
        public static PffFile Open(FileInfo file, Encoding enc = null)
        {
            enc = enc ?? Encoding.GetEncoding(Common.NlCodepage);

            if (file == null || string.IsNullOrEmpty(file.FullName))
                throw new ArgumentException("File cannot be null or empty.", nameof(file));

            if (!file.Exists)
                throw new FileNotFoundException($"File '{file.FullName}' not found.");

            if (!FileSystemHelper.CanReadFile(file.FullName))
                throw new UnauthorizedAccessException($"Cannot open '{file.FullName}' due to insufficient permission.");

            var pff = new PffFile(file, enc);

            return PffReader.Read(pff);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static PffFile Create(PffGameInfo game, Encoding enc = null)
        {
            enc = enc ?? Encoding.GetEncoding(Common.NlCodepage);

            var version = PffVersion.FromGame(game, enc);

            // Version must be set before constructing child classes so they can reference Pff, Enc, etc.
            var pff = new PffFile(enc)
            {
                Version = version,
                Header = new PffHeader(version)
            };

            pff.EntryTable = new PffEntryTable(pff);
            pff.Footer = new PffFooter(version);

            return pff;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public PffFile Save(string destFile, bool preserveDeadSpace = true)
        {
            if (string.IsNullOrEmpty(destFile))
                throw new ArgumentException("File path cannot be empty.", nameof(destFile));

            var fileExists = File.Exists(destFile);

            switch (fileExists)
            {
                case true when !FileSystemHelper.CanWriteFile(destFile):
                    throw new UnauthorizedAccessException($"Cannot write to '{destFile}' due to insufficient permission.");
                case false when !FileSystemHelper.CanCreateFile(destFile, FileOptions.None):
                    throw new UnauthorizedAccessException($"Cannot create '{destFile}' due to insufficient permission.");
                default:
                    DestFile = new FileInfo(destFile);
                    break;
            }

            // Validate required structures are populated
            if (Header == null)
                throw new InvalidOperationException("Header must be populated before saving.");

            if (Version == null)
                throw new InvalidOperationException("Version must be populated before saving.");

            if (EntryTable == null)
                throw new InvalidOperationException("Entry table must be populated before saving.");

            if (Footer == null)
                throw new InvalidOperationException("Footer must be populated before saving.");

            if (!preserveDeadSpace)
                EntryTable.Entries.RemoveAll(e => e.DeadSpace || e.DataSize == 0);

            // Write
            PffWriter.Write(this);

            // After saving, set SourceFile from DestFile
            SourceFile = new FileInfo(DestFile.FullName);
            DestFile = null;

            return this;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Import / Export

        public void ImportEntry(FileInfo file, bool overwriteExisting = false)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be empty.");

            if(!file.Exists)
                throw new FileNotFoundException($"File '{file.FullName}' not found.");

            if (!FileSystemHelper.CanReadFile(file.FullName))
                throw new UnauthorizedAccessException($"Cannot read '{file.FullName}' due to insufficient permission.");

            if (file.Length <= 0)
                throw new InvalidDataException($"File '{file.Name}' cannot be zero bytes.");

            // Check size limit (2GB is the max for the byte array from ReadAllBytes)
            if (file.Length > int.MaxValue)
                throw new InvalidDataException($"File '{file.Name}' cannot be greater than 2GB.");

            if(file.Length + TotalDataSize > int.MaxValue)
                throw new InvalidDataException($"Importing '{file.Name}' would exceed maximum total data size (2GB).");

            var validFileName = FileNameHelper.ValidFileName(file.Name, new FileNameHelper.ValidationOptions
            {
                MaxLength = new FileNameHelper.FileNameRule<int>(15),
                EnforceAscii = new FileNameHelper.FileNameRule<bool>(true)
            });

            if (!validFileName.IsValid)
                throw new FileNameException($"Invalid filename '{file.Name}'{Environment.NewLine}{Environment.NewLine}{validFileName.Message}");

            // Get any existing entry with the same name
            var existingEntry = EntryTable.GetEntry(file.Name);

            // If the entry exists, and we're not overwriting return
            if (existingEntry != null)
            {
                if (!overwriteExisting)
                    throw new FileExistsException($"File '{file.Name}' already exists in the pff.");

                EntryTable.SetEntryDeadSpace(existingEntry);
            }

            var entry = new PffEntry(EntryTable.GetNextId(), this)
            {
                FileNameBytes = Enc.GetBytes(file.Name),
            };

            var fileTimestamp = File.GetLastWriteTimeUtc(file.FullName);
            var now = DateTime.UtcNow;
            var timestamp = (fileTimestamp > now ? now : fileTimestamp).ToUnixTimestamp();
            EntryTable.SetEntryData(entry, File.ReadAllBytes(file.FullName), timestamp);

            EntryTable.AddEntry(entry);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public void ExportEntry(string destDir, PffEntry entry)
        {
            if (string.IsNullOrEmpty(destDir))
                throw new ArgumentException("Destination directory cannot be empty.", nameof(destDir));

            if (!FileSystemHelper.CanWriteDirectory(destDir))
                throw new DirectoryNotFoundException($"Destination directory '{destDir}' not found or no permission.");

            if (entry == null || entry.DataSize == 0 || entry.DeadSpace)
                throw new InvalidOperationException($"Entry '{entry?.FileNameStr}' cannot be exported because it is invalid or empty.");

            if (string.IsNullOrEmpty(entry.FileNameSanitized))
                throw new InvalidOperationException($"Entry '{entry.FileNameStr}' cannot be exported because it has an invalid name.");

            // Uses a sanitized file name to prevent issues
            var fullPath = Path.Combine(destDir, entry.FileNameSanitized);

            try
            {
                File.WriteAllBytes(fullPath, entry.Data);
            }
            catch (Exception ex)
            {
                throw new IOException($"Entry '{entry.FileNameStr}' cannot be exported.{Environment.NewLine}{Environment.NewLine}{ex.Message}", ex);
            }

            // Restore timestamp, will fail silently if unable
            FileSystemHelper.SetFileTimestamp(fullPath, entry.DateTimeUtc, true);
        }
        #endregion

    }

}