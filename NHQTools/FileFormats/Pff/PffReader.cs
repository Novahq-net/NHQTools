using System.IO;

namespace NHQTools.FileFormats.Pff
{
    internal static class PffReader
    {
        // Reads all entry data into memory immediately, including calculating CRCs
        // for each entry.
        internal static PffFile Read(PffFile pff)
        {
            var enc = pff.Enc;
            var sourceFile = pff.SourceFile.FullName;


            using (var stream = File.Open(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new BinaryReader(stream, enc))
            {

                // The minimum header size is 20 bytes
                if (stream.Length < 20)
                    throw new InvalidDataException($"Unsupported Header length: {stream.Length}");

                var header = PffHeader.Read(reader);
                var version = PffVersion.FromHeader(header, enc);

                // Set Version early so child classes (PffEntryTable, PffEntry) can derive it from pffFile
                pff.Header = header;
                pff.Version = version;

                // Validate we have enough file to read the entry table
                if (header.EntryTableOffset + header.EntryRecordLength * header.EntryTableCount > stream.Length)
                    throw new EndOfStreamException("Entry table exceeds stream length");

                var entryTable = PffEntryTable.Read(reader, header, pff);

                var footer = PffFooter.Read(reader, version);

                // Populate remaining PffFile structures
                pff.EntryTable = entryTable;
                pff.Footer = footer;

                // Entry Data
                foreach (var entry in entryTable.Entries)
                {
                    stream.Position = entry.DataOffset;

                    if (entry.DataSize > int.MaxValue)
                        throw new InvalidDataException($"Entry '{entry.FileNameStr}' data size exceeds maximum supported size: {entry.DataSize}");

                    // I debated lazy loading here, but I want to calculate CRCs right away so I have to read the data
                    // I could dispose of the data after calculating CRCs, but that seems like a waste. PPF files are usually not that large.
                    // Google says I can just "download more RAM" if needed

                    var size = (int)entry.DataSize;
                    var buffer = new byte[size];
                    var totalRead = 0;

                    while (totalRead < size)
                    {
                        var n = stream.Read(buffer, totalRead, size - totalRead);

                        if (n == 0) 
                            break;

                        totalRead += n;
                    }
                    entry.Data = buffer;
                }

                // Rebuild the filter dropdown cache
                entryTable.BuildFilterCache();
            }

            return pff;

        }

    }

}