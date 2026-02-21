using System.IO;

namespace NHQTools.FileFormats.Pff
{
    internal static class PffWriter
    {
        ////////////////////////////////////////////////////////////////////////////////////
        internal static void Write(PffFile pff)
        {

            var enc = pff.Enc;
            var destFile = pff.DestFile.FullName;
            var header = pff.Header;
            var version = pff.Version;
            var entryTable = pff.EntryTable;
            var footer = pff.Footer;

            using (var stream = new FileStream(destFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            using (var writer = new BinaryWriter(stream, enc))
            {

                // Set position directly past header to start writing entry data blobs
                // We write the header at the end after final offsets are known
                // We use the constant here to enforce a fixed header size regardless of supplied header data
                writer.BaseStream.Position = PffHeader.Length;

                // Write data blobs
                foreach (var entry in entryTable.Entries)
                {

                    // Allow writing files that contain 0 data size because some PFFs have entries with 0 size (LW-Mods.pff)
                    // if (entry.Data == null)
                    //   throw new InvalidDataException("Entry data cannot be empty");

                    // Update entry offset — DataSize is kept in sync by the Data setter
                    entry.DataOffset = (uint)writer.BaseStream.Position;

                    if (entry.DataSize > 0)
                        // ReSharper disable once AssignNullToNotNullAttribute
                        writer.Write(entry.Data);
                }

                // Current values for header to make sure everything is aligned
                var entryTableOffset = (uint)writer.BaseStream.Position;
                var entryTableCount = entryTable.EntryCount;

                // Write remaining structures
                PffEntryTable.Write(writer, entryTable, version, enc);
                PffFooter.Write(writer, footer, version, enc);
                PffHeader.Write(writer, header, entryTableCount, entryTableOffset);

            }

        }

    }

}