using System;
using System.Text;

// NHQTools Libraries
using NHQTools.Helpers;
using NHQTools.Extensions;

namespace NHQTools.FileFormats.Pff
{
    public class PffEntry
    {
        private static readonly byte[] EmptyByte = Array.Empty<byte>();

        // PffFile Ref
        public PffFile Pff { get; }
        public Encoding Enc => Pff?.Enc; // Convenience accessor for Pff encoding
        public PffVersion Version => Pff?.Version; // Convenience accessor for Pff version

        // Entry Identification
        public int Id { get; internal set; }  // We could use offset, but it's possible for two entries to have the same offset (0 size entries, new entries, etc.)

        public FileType FileType { get; internal set; }

        public string FileTypeExt { get; internal set; } // Set by FileNameBytes setter

        // Entry Table Metadata
        public uint EntryTableOffset { get; internal set; }  // Absolute offset to the ***EntryTable*** record (NOT DATA BLOB)
        public byte[] EntryTableRawBytes { get; internal set; } // Raw bytes of the ***EntryTable*** record (NOT DATA BLOB)

        ////////////////////////////////////////////////////////////////////////////////////
        #region Dead Space
        public uint DeadSpaceFlags // Will trigger re-evaluation of DeadSpace
        {
            get => _deadSpaceFlags;
            internal set
            {
                _deadSpaceFlags = value;
                _deadSpace = null;
            }
        } 
        private uint _deadSpaceFlags;

        public bool DeadSpace // Checks both the flags and the filename string
        {
            get
            {
                if (!_deadSpace.HasValue)
                    _deadSpace = (DeadSpaceFlags > 0 && (DeadSpaceFlags & 1) != 0)
                                 || string.Equals((FileNameStr ?? string.Empty).Trim(), "<DEAD SPACE>", StringComparison.OrdinalIgnoreCase);
                return _deadSpace.Value;
            }
        }
        private bool? _deadSpace;
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Data Blob
        public uint DataOffset { get; internal set; } // Absolute offset to entry's data blob
        public uint DataSize
        {
            get => _dataSize;
            internal set
            {
                _dataSize = value;
                DataSizeStr = value.ToFileSize(); // Cache the file size string for UI
            }
        }
        private uint _dataSize;

        public string DataSizeStr { get; private set; } // Set when DataSize is updated. Name must be kept in sync with PffGrid in ***mapcols AND onCompare***

        // Triggers CrcComputed, DataSize sync, and FileType detection
        // This keeps all data-dependent fields in sync automatically
        internal byte[] Data
        {
            get => _data ?? EmptyByte;
            set
            {
                _data = value;

                // Empty data is allowed for zero-length files in existing PFFs (LW-AllMods)
                if (value == null || value.Length == 0)
                {
                    DataSize = 0;
                    CrcComputed = null;
                    CrcRead = null;
                    FileType = FileType.Unknown;
                    return;
                }

                DataSize = (uint)value.Length;

                // CRC before FileType detection
                // *** DO NOT*** update CrcRead here,
                // it should only be updated when we explicitly set new data
                var crcType = Version?.EntryCrcType ?? EntryCrcTypes.None;

                switch (crcType)
                {
                    case EntryCrcTypes.Pff2_3:
                        CrcComputed = PffCrc.ComputePff2_3(value, 0, value.Length);
                        break;
                    case EntryCrcTypes.Pff4:
                        CrcComputed = PffCrc.ComputePff4(value, 0, value.Length);
                        break;
                    case EntryCrcTypes.None:
                    default:
                        CrcComputed = null;
                        break;
                }

                FileType = value.Length < 2 || DeadSpace ? FileType.Unknown : Definitions.DetectType(value, FileNameStr);
            }
        }
        private byte[] _data;
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region File Name
        // Setting FileNameBytes triggers parsing of the file name string
        // This keeps everything in sync when the file name has changed
        // We don't allow setting file name strings directly to avoid desyncs
        // ***FileNameBytes will always be padded/truncated to max length***
        internal byte[] FileNameBytes
        {
            get => _fileNameBytes;
            set
            {

                // Do not allow null or empty filenames. We could allow them,
                // but it makes no sense, and I've not encountered a PFF entry with no name yet 
                if (value == null || value.Length <= 0)
                    throw new ArgumentException("Filename cannot be null or empty", nameof(value));

                // Always pad/truncate to max length + null terminator
                _fileNameBytes = value.RPadTruncate(PffVersion.FileNameLength + 1);

                // Re-parse filenames
                FileNameStr = _fileNameBytes.ReadCString(0, Enc, PffVersion.FileNameLength);
                FileNameSanitized = FileNameHelper.SanitizeAsciiFileName(FileNameStr, 15);

                // Extension
                var ext = FileNameStr.LastIndexOf('.');
                FileTypeExt = ext >= 0 ? FileNameStr.Substring(ext) : string.Empty;

                // Sync the editable binding property so the grid reflects the authoritative value
                FileNameEdit = FileNameStr;

                // Clear the cached, did someone managed to type <DEAD SPACE> after all?
                _deadSpace = null;
            }
        }
        private byte[] _fileNameBytes;

        // FileNameStr is read-only to consumers, see FileNameEdit comment
        public string FileNameStr { get; private set; } // Only set via FileNameBytes
        public string FileNameSanitized { get; private set; } // Only set via FileNameBytes

        // FileNameEdit is the dummy binding for DataGridView for cell editing.
        // ***The grid writes user input here, but it is NOT the source of truth***
        // The actual rename goes through PffFile.RenameEntry > FileNameBytes setter,
        // which syncs FileNameStr, FileNameSanitized, and FileNameEdit back to the authoritative value.
        public string FileNameEdit { get; set; }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region CRC
        public uint? CrcRead { get; internal set; } // CrcRead can be null, and can be set by importing via grid
        public uint? CrcComputed { get; private set; }
        public bool CrcMatches => !CrcRead.HasValue || !CrcComputed.HasValue || CrcRead.Value == CrcComputed.Value;
        public string CrcStr => !DeadSpace && (CrcRead.HasValue || CrcComputed.HasValue)
            ? CrcMatches ? (CrcRead ?? CrcComputed).ToString() : (CrcRead ?? CrcComputed) + "*"
            : "N/A";
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Timestamp
        public uint Timestamp // Will clear cached DateTime values
        {
            get => _timestamp;
            internal set
            {
                _timestamp = value;
                _dateTimeUtc = null;
                _dateTimeLocal = null;
            }
        }
        private uint _timestamp;

        public DateTime DateTimeUtc
        {
            get
            {
                if (!_dateTimeUtc.HasValue)
                    _dateTimeUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Timestamp);
                return _dateTimeUtc.Value;
            }
            internal set
            {
                _dateTimeUtc = value;
                _dateTimeLocal = null; // We derive local time from UTC, so clear cached local time if we update this
            }
        }
        private DateTime? _dateTimeUtc;

        public DateTime DateTimeLocal
        {
            get
            {
                if (!_dateTimeLocal.HasValue)
                    _dateTimeLocal = DateTimeUtc.ToLocalTime();
                return _dateTimeLocal.Value;
            }
        }
        private DateTime? _dateTimeLocal;

        public string DateTimeUtcStr => DateTimeUtc.ToString("yyyy/MM/dd hh:mm tt");
        public string DateTimeLocalStr => DateTimeLocal.ToString("yyyy/MM/dd hh:mm tt");
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        internal PffEntry(int id, PffFile pff)
        {
            Id = id;
            Pff = pff;
        }

    }

}