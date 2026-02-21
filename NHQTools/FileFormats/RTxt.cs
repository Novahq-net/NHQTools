using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    ////////////////////////////////////////////////////////////////////////////////////
    // After building this, I realized that there is a text tool included in the BHD mod
    // tools that builds these files. Eventually I will probaby adjust this format to
    // match that tool, but I don't have a good reason to do that right now.
    ////////////////////////////////////////////////////////////////////////////////////
    public static class RTxt
    {
        // Public
        public const int HeaderLen = 16;
        public const int MinExpectedLen = HeaderLen + 2;

        // Debug
        public static bool DebugWriteStrings { get; set; } = false; // Toggle to reduce debug output

        // Options (Preserves original byte structure so we can verify encode/decode is lossless
        public static bool ValTableReadPastFirstNull { get; set; } = true; // Read notes about this in Decode()
        public static char ValTableNullReplaceWithChar { get; set; } = '␀'; // Symbol to represent null chars

        // Constants
        private const int SZ_GROUP_COUNT = 4;
        private const int SZ_GROUP_HEADER = 8;
        private const int SZ_ENTRY_TABLE = 16;

        // Regex (Compiled once, reused across calls)
        private static readonly Regex RxOffsets = new Regex(
            @"^\s*\[\s*x\s*[=:]\s*(-?\d+)\s*,\s*y\s*[=:]\s*(-?\d+)\s*\]", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RxTokens = new Regex(
            // Skip whitespace/newlines between tokens
            @"\s*(?:" +

            // Comments (Match // until end of line)
            "(?<comment>//.*$)|" +

            // [Group Name]
            @"\[(?<group>[^\]]*)\]|" +

            // "Key" = "Value"; Look for the closing quote and semicolon.
            @"""(?<key>(?:\\.|[^""])*)""\s*=\s*""(?<val>(?:\\.|[^""])*)""\s*;|" +

            // "Value Only";
            @"""(?<valOnly>(?:\\.|[^""])*)""\s*;" +

            ")", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture
        );

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.GetEncoding(Common.NlCodepage);

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static RTxt() => Def = Definitions.GetFormatDef(FileType.RTXT);

        ////////////////////////////////////////////////////////////////////////////////////
        #region To Text

        public static string ToTxt(FileInfo file, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : ToTxt(File.ReadAllBytes(file.FullName), format, enc);
        }
        public static string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (fileData == null || fileData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            if (!Def.SerializeFormats.Contains(format))
                throw new ArgumentException("Unsupported serialization format.", nameof(format));

            var rtxt = Decode(fileData, enc);

            return SerializeToText(rtxt, format);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region From Text
        public static byte[] FromTxt(FileInfo file, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : FromTxt(File.ReadAllText(file.FullName, enc), format, enc);
        }

        public static byte[] FromTxt(string textData, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (string.IsNullOrEmpty(textData))
                throw new ArgumentException("Text cannot be empty.", nameof(textData));

            if (!Def.SerializeFormats.Contains(format))
                throw new ArgumentException("Unsupported serialization format.", nameof(format));

            var rtxt = ParseText(textData, format, enc);

            return Encode(rtxt);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decode
        private static RTxtFile Decode(byte[] fileData, Encoding enc)
        {
            var reader = new ByteReader(fileData);

            // Header
            var magic = reader.ReadBytes(Def.MagicBytes.Length);
            var groupTableOffset = reader.ReadInt32();
            var groupTableLength = reader.ReadInt32();
            var refTableCount = reader.ReadInt32();

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            // If any offsets/lengths are negative
            if (groupTableOffset < 0 || groupTableLength < 0 || refTableCount < 0)
                throw new InvalidDataException("The file is corrupt or an unsupported format (invalid group table).");

            // Validate ref table fits in file
            if (refTableCount * SZ_ENTRY_TABLE >= reader.Length || refTableCount * SZ_ENTRY_TABLE > groupTableOffset)
                throw new InvalidDataException("The file is corrupt or an unsupported format (invalid reference table).");

            // Validate Group Table fits in file (it should run to EOF)
            // If group table has a length, it's offset + group count + group length should equal file length
            // If group table length is 0, only offset + group count should equal file length
            if ((groupTableLength > 0 && (SZ_GROUP_COUNT + groupTableLength + groupTableOffset) != reader.Length)
                 || (groupTableLength == 0 && (SZ_GROUP_COUNT + groupTableOffset) != reader.Length))
            {
                Debug.WriteLine(new string('-', 50));
                Debug.WriteLine("RTXT Invalid Header (These should match, data.Legnth should == groupTableLength + groupTableOffset):");
                Debug.WriteLine($"groupTableOffset: {groupTableOffset}");
                Debug.WriteLine($"groupTableLength: {groupTableLength}");
                Debug.WriteLine($"Expected: {reader.Length}");
                Debug.WriteLine($"Read: {groupTableOffset + groupTableLength}");

                // We can't throw an exception because some RTXT files have invalid lengths in the header.
                //throw new InvalidDataException("The file is corrupt or an unsupported format (group table should reach EOF).");
            }

            // Read
            var rtxt = new RTxtFile { Enc = enc };

            // Group count is after the Group Table Offset
            var groupCount = reader.PeekInt32(groupTableOffset);

            // Total length of all group headers (for fix)
            var groupHeadersLength = groupCount * SZ_GROUP_HEADER;

            // So we don't have to keep adding SZ_GROUP_COUNT
            var currentGroupTableOffset = groupTableOffset + SZ_GROUP_COUNT;

            // Group Names start after Group Headers
            var groupNamesAbsOffset = currentGroupTableOffset + (groupCount * SZ_GROUP_HEADER);

            // Peek Group Names
            var groupNames = reader.PeekStrings(groupNamesAbsOffset, groupCount);

            // Read Group Headers
            reader.Seek(currentGroupTableOffset);

            for (var i = 0; i < groupCount; i++)
            {
                var keysOffset = reader.ReadInt32(); // Relative to start of Group Table + 4
                var entryCount = reader.ReadInt32();

                // Create Group
                var group = new RtxtGroup(i, string.IsNullOrEmpty(groupNames[i]) ? null : groupNames[i]); // NULL FIX

                // Add entries with Keys (Values, etc. come later)
                var keysAbsOffset = currentGroupTableOffset + keysOffset;

                string[] fixedKeys = null;
                ////////////////////////////////////////////////////////////////////////////////////
                // ***** Fixup for invalid key offsets in C4 mission files *****
                // Some C4 RTXT files have invalid key offsets that point inside the group header table.
                // Hopefully we don't hate ourselves later for this...
                // We only need to check the first group, and it only applies if KeysTableRelOffset < groupHeaderTotalLength
                ////////////////////////////////////////////////////////////////////////////////////
                if (i == 0 && keysOffset < (SZ_GROUP_HEADER * groupCount) )
                {

                    // Corrected offset will be ((groupCount * SZ_GROUP_HEADER) + sum of group name lengths)
                    var correctedOffset = groupHeadersLength + groupNames.Sum(n => n.GetByteCount(enc));
                    fixedKeys = reader.PeekStrings(currentGroupTableOffset + correctedOffset, entryCount);

                    Debug.WriteLine(new string('-', 50));
                    Debug.WriteLine($"Invalid: key offset. Offset is > group header  '{keysOffset}' > '{groupHeadersLength}'.");
                    Debug.WriteLine($"Corrected offset: '{correctedOffset}'.");

                    foreach (var k in fixedKeys)
                        Debug.WriteLine($"  {k}");

                }
    
                var keys = fixedKeys ?? reader.PeekStrings(keysAbsOffset, entryCount);

                // Add entries
                foreach (var key in keys)
                    group.Entries.Add(new RtxtEntry { Key = string.IsNullOrEmpty(key) ? null : key }); // NULL FIX
 
                rtxt.Groups.Add(group);
            }

            // If no groups exist, we still need one to hold the values
            if (rtxt.Groups.Count == 0)
            {
                // Create a dummy group with enough null slots for the ref table
                var nullGroup = new RtxtGroup(-1, null);

                for (var i = 0; i < refTableCount; i++) 
                    nullGroup.Entries.Add(null);

                rtxt.Groups.Add(nullGroup);
            }

            // Read Reference Table and link Values to Entries
            // Ref Table starts immediately after the 16-byte header
            // Values match with keys by order they appear in the Ref Table
            reader.Seek(HeaderLen);

            var valuesBaseOffset = HeaderLen + (refTableCount * SZ_ENTRY_TABLE);

            // Track how many entries we've filled per group
            var groupEntryCounters = new int[rtxt.Groups.Count];

            for (var i = 0; i < refTableCount; i++)
            {
                var valOffset = reader.ReadInt32(); // Relative to Values Base Offset

                var valOffsetX = reader.ReadInt16(); // Positioning info (short)

                var valOffsetY = reader.ReadInt16(); // Positioning info (short)

                var groupId = reader.ReadInt16(); // 0xFFFF = -1 for value-only files (short)

                reader.Skip(6); // Padding

                //////////////////////////////////////////////////////////////////////
                #region Peek string with nulls 

                string fullVal = null;
                if (ValTableReadPastFirstNull)
                {
                    if(i == 0 && DebugWriteStrings) {
                        Debug.WriteLine(new string('-', 50));
                        Debug.WriteLine("Value Table Read Past Nulls");
                    }
                    //////////////////////////////////////////////////////////////////////
                    // *** PRESERVES NULLS SO WE CAN VALIDATE ENCODE/DECODE WRITE IDENTICAL BYTES ***
                    // Some files have consecutive nulls which would be lost if we used PeekString()
                    // This is probably garbage data left over from buffer allocation, but 
                    // for our testing we want to preserve exact byte structure so we can 
                    // verify encode/decode is lossless.
                    //////////////////////////////////////////////////////////////////////
                    var fullValOffNext = (i == refTableCount - 1)
                        ? groupTableOffset - valuesBaseOffset
                        : reader.PeekInt32(reader.Position);

                    // Length = End - Start - 1 (for null terminator)
                    var fullStrLength = Math.Max(0, fullValOffNext - valOffset - 1);
                    var fullCurrentOffset = valuesBaseOffset + valOffset;    

                    // Read exact bytes using the calculated length
                    fullVal = enc.GetString(reader.Data, fullCurrentOffset, fullStrLength).Replace('\0', ValTableNullReplaceWithChar);

                    if (DebugWriteStrings)
                    {
                        Debug.WriteLine(new string('-', 20));
                        Debug.WriteLine($"Index: {i,-4} Relative Base: {valuesBaseOffset,-5} Offset: {valOffset,-5} Next: {fullValOffNext,-5} Length: {fullStrLength,-5}");
                        Debug.WriteLine($" \"{fullVal}\"");
                    }

                    //////////////////////////////////////////////////////////////////////
                    // Peek string (Debug Only END)
                    //////////////////////////////////////////////////////////////////////
                }
                #endregion

                // Read Value String
                var value = fullVal ?? reader.PeekString(valuesBaseOffset + valOffset);

                // Handling Value-Only files (GroupId is usually -1)
                if (groupId == -1) 
                    groupId = 0; // index map to value-only group

                if (groupId >= rtxt.Groups.Count)
                    throw new InvalidDataException($"Invalid group '{groupId}' in Reference Table.");

                var group = rtxt.Groups[groupId];
                var entryIndex = groupEntryCounters[groupId];

                // If the value-only group was created via GroupTable, it already has an entry with a Key but no Value.
                // If it's a value-only group, the entry starts as null.
                if (entryIndex < group.Entries.Count && group.Entries[entryIndex] != null)
                {
                    // Existing entry with key
                    group.Entries[entryIndex].Value = value;
                    group.Entries[entryIndex].OffsetX = valOffsetX;
                    group.Entries[entryIndex].OffsetY = valOffsetY;
                }
                else
                {
                    // Value-only entry
                    var entry = new RtxtEntry(null, value, valOffsetX, valOffsetY);

                    if (entryIndex < group.Entries.Count)
                        group.Entries[entryIndex] = entry; // Fill null
                    else
                        group.Entries.Add(entry); // This will never happen right?
                }

                groupEntryCounters[groupId]++;
            }

            // Remove null entries from value-only group
            if (rtxt.Groups[0].Id == -1)
                rtxt.Groups[0].Entries.RemoveAll(x => x == null);

            return rtxt;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Encode
        private static byte[] Encode(RTxtFile rtxtObj)
        {
            var enc = rtxtObj.Enc;

            var groups = rtxtObj.Groups;
            var groupCount = groups.Count;

            // Linear list of all entries to build the Reference Table
            // Item1 = GroupId, Item2 = Entry, <GroupId, Entry> 
            var totalEntries = groups.Sum(g => g.Entries.Count);
            var flatEntries = new List<Tuple<int, RtxtEntry>>(totalEntries);
            var valuesOnly = groupCount == 1 && string.IsNullOrEmpty(groups[0].Section);

            for (var i = 0; i < groupCount; i++)
            {
                foreach (var entry in groups[i].Entries)
                {
                    // If ID is -1, write -1, else write index
                    var groupId = valuesOnly ? -1 : i;
                    flatEntries.Add(new Tuple<int, RtxtEntry>(groupId, entry));
                }
            }

            // For readability
            var entries = flatEntries.Select(t => new {
                GroupId = t.Item1,
                Item = t.Item2
            }).ToList();

            var refTableLen = flatEntries.Count * SZ_ENTRY_TABLE;

            // Value Strings Table (Inclues null)
            var valTableLen = flatEntries.Sum(x => x.Item2.Value.GetByteCount(enc));

            // Group Table Absolute Offset (Header + RefTable + ValTable)
            var groupTableOffset = HeaderLen + refTableLen + valTableLen;

            // Group Table Contents Size
            var groupHeadersLen = valuesOnly ? 0 : groupCount * SZ_GROUP_HEADER;
            var groupNamesLen = valuesOnly ? 0 : groups.Sum(g => g.Section.GetByteCount(enc));
            var groupKeysLen = valuesOnly ? 0 : groups.Sum(g => g.Entries.Sum(x => x.Key.GetByteCount(enc)));

            // Total Group Table Length
            var groupTableLength = groupHeadersLen + groupNamesLen + groupKeysLen;

            // Write
            var writer = new ByteWriter(groupTableOffset + SZ_GROUP_COUNT + groupTableLength, enc);

            // Header
            writer.Write(Def.MagicBytes);
            writer.Write(groupTableOffset);
            writer.Write(groupTableLength);
            writer.Write(flatEntries.Count);

            // Reference Table
            var currentValOffset = 0;
            var refPadding = new byte[6];

            foreach (var entry in entries)
            {
                writer.Write(currentValOffset);
                writer.Write(entry.Item.OffsetX); // Positioning info (short)
                writer.Write(entry.Item.OffsetY); // Positioning info (short)
                writer.Write((short)entry.GroupId); // Group ID
                writer.Write(refPadding); // Padding

                // Increment offset for next entry (GetByteCount includes null)
                currentValOffset += entry.Item.Value.GetByteCount(enc);
            }

            // Value Strings
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var entry in entries)
            {
                var val = entry.Item.Value;

                // Debug: Replace null chars with null for writing back
                if (ValTableReadPastFirstNull)
                    val = val.Replace(ValTableNullReplaceWithChar, '\0');
            
                writer.WriteCString(val); // Write includes null
            }

            // Group Count
            writer.Write(valuesOnly ? 0 : groupCount);

            // If no groups, we're done
            if (valuesOnly) 
                return writer.ToArray();

            // Group Headers
            var currentKeyRelOffset = groupHeadersLen + groupNamesLen;

            foreach (var group in groups)
            {
                writer.Write(currentKeyRelOffset); // Offset to current group keys
                writer.Write(group.Entries.Count);

                // Increment offset by groups total key length
                currentKeyRelOffset += group.Entries.Sum(x => x.Key.GetByteCount(enc));
            }

            // Group Names
            foreach (var group in groups)
                writer.WriteCString(group.Section); // Write includes null

            // Group Keys
            foreach (var entry in groups.SelectMany(g => g.Entries))
            {
                // Skip writing null keys
                if (entry.Key == null)
                    writer.Position += 1;
                else
                    writer.WriteCString(entry.Key); // Write includes null
            }

            return writer.ToArray();
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Parse
        private static RTxtFile ParseText(string textData, SerializeFormat format, Encoding enc) =>
            format == SerializeFormat.JSON ? ParseJson(textData, enc) : ParseIni(textData, enc);

        ////////////////////////////////////////////////////////////////////////////////////
        private static RTxtFile ParseJson(string textData, Encoding enc)
        {
            if (string.IsNullOrEmpty(textData))
                throw new ArgumentException("Text cannot be empty.", nameof(textData));

            // Deserialize
            var rtxt = textData.Parse<RTxtFile>();

            if (rtxt == null)
                throw new InvalidDataException("Failed to parse JSON.");

            // Restore the Encoding
            rtxt.Enc = enc;

            // If groups are missing we can't proceed
            return rtxt.Groups == null
                ? throw new NotSupportedException("Failed to parse JSON. (No groups defined)")
                : rtxt;

        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static RTxtFile ParseIni(string textData, Encoding enc)
        {
            if (string.IsNullOrEmpty(textData))
                throw new ArgumentException("Text cannot be empty.", nameof(textData));

            // Strip nulls from text to stop our regexes from breaking
            textData = textData.Replace("\0", "");

            // Find all matches in the text
            var tokenMatches = RxTokens.Matches(textData);

            if (tokenMatches.Count == 0)
                throw new InvalidDataException("Text data is empty or invalid.");

            // Determine format based on first non-comment match
            var firstRealMatch = tokenMatches.Cast<Match>().FirstOrDefault(m => !m.Groups["comment"].Success);

            // No valid matches found
            if (firstRealMatch == null)
                throw new InvalidDataException("No valid data found.");

            // If first match is a group name, we are in grouped format
            var isGroups = firstRealMatch.Groups["group"].Success;

            // Parse matches into groups and entries
            var groups = new List<RtxtGroup>();
            RtxtGroup group = null;

            var groupId = -1;


            foreach (var items in tokenMatches.IncludeLineNo(textData))
            {
                var match = items.Match;
                var lineNo = items.LineNo;

                // Skip comments
                if (match.Groups["comment"].Success)
                    continue;

                // [GROUP]
                if (match.Groups["group"].Success)
                {

                    if (!isGroups)
                    {
                        // Verbose Debugging Info
                        var msg = new StringBuilder();
                        msg.AppendLine($"[Line {lineNo}] Parsed a '[Group]' header in a Value-only file.");
                        msg.AppendLine($"Matched Text: [{match.Value.Trim()}]");
                        msg.AppendLine("Possible Causes:");
                        msg.AppendLine("1. This file started with loose values (implying 'ValueOnly' or 'NoGroups' format) but later introduced a [Group].");
                        msg.AppendLine("2. You cannot mix ungrouped values at the top of the file with grouped sections later.");

                        throw new InvalidDataException(msg.ToString());
                    }

                    groupId++;

                    var groupName = match.Groups["group"].Value.Trim();

                    group = new RtxtGroup(groupId, groupName);
                    groups.Add(group);
                    continue;
                }

                // Create default group for value-only format
                if (group == null)
                {
                    group = new RtxtGroup(groupId, null);
                    groups.Add(group);
                }

                string key = null;
                string val = null;

                // Key = Value pair
                if (match.Groups["key"].Success)
                {
                    if (!isGroups)
                    {
                        // Verbose Debugging Info
                        var msg = new StringBuilder();
                        msg.AppendLine($"[Line {lineNo}] Parsed a 'Key-Value' token in a Value-only file.");
                        msg.AppendLine($"Matched Key: \"{match.Groups["key"].Value}\"");
                        msg.AppendLine($"Matched Value: \"{match.Groups["val"].Value}\"");
                        msg.AppendLine("Possible Causes:");
                        msg.AppendLine("1. This file contains Key=Value pairs, but didn't start with a [Group] header.");
                        msg.AppendLine("2. You have a mixed format file (not supported).");
                        msg.AppendLine("3. You have an unescaped quote that made a Value look like a Key.");

                        throw new InvalidDataException(msg.ToString());
                    }

                    key = match.Groups["key"].Value; // Do NOT Trim() keys/values
                    val = match.Groups["val"].Value; // Do NOT Trim() keys/values
                }

                // Value only
                else if (match.Groups["valOnly"].Success)
                {
                    if (isGroups)
                    {
                        // Verbose Debugging Info
                        var msg = new StringBuilder();
                        msg.AppendLine($"[Line {lineNo}] Parsed a 'Value Only' token in a Key-Value file.");
                        msg.AppendLine($"Matched Text: {match.Value}");
                        msg.AppendLine($"Parsed Value: {match.Groups["valOnly"].Value}");
                        msg.AppendLine("Possible Causes:");
                        msg.AppendLine("1. You have a stray string not assigned to a key.");
                        msg.AppendLine("2. The 'Key = Value' regex failed to match this line (check for missing '=' or unescaped quotes).");

                        throw new InvalidDataException(msg.ToString());
                    }

                    val = match.Groups["valOnly"].Value; // Do NOT Trim() keys/values
                }

                // Continue if group is empty (no key or value)
                if (val == null)
                    continue;

                // Debug replace null chars if needed
                if (ValTableReadPastFirstNull)
                {
                    val = val.Replace(ValTableNullReplaceWithChar, '\0');
                    key = key?.Replace(ValTableNullReplaceWithChar, '\0');
                }

                // UnEscape string
                val = val.Replace("\\\"", "\"");
                key = key?.Replace("\\\"", "\"");

                // Strip offsets from start of value
                var realValue = val;
                short offsetX = 0;
                short offsetY = 0;

                // Matches: [x=10,y=20] OR [x: 10, y: 20]
                var offsetsMatch = RxOffsets.Match(val);

                if (offsetsMatch.Success)
                {
                    // Parse the x/y values, if parsing fails we just leave them as 0 and keep the value as-is
                    if (!short.TryParse(offsetsMatch.Groups[1].Value, out offsetX) ||
                        !short.TryParse(offsetsMatch.Groups[2].Value, out offsetY))
                    {
                        Debug.WriteLine($"[Line {lineNo}] Offset parse failed: {offsetsMatch.Value}");
                    }

                    // Remove offsets from value, just strip the matched part since we want to preserve any whitespace after it
                    realValue = val.Substring(offsetsMatch.Length);
                }

                group.Entries.Add(new RtxtEntry(key, realValue, offsetX, offsetY));
            }

            // Build RTxtFile
            return new RTxtFile
            {
                Enc = enc,
                Groups = groups
            };
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Formatting

        private static string SerializeToText(RTxtFile rtxt, SerializeFormat format)
        {

            // JSON 
            if (format == SerializeFormat.JSON)
                return rtxt.Stringify(prettyPrint:true);

            // INI 
            var sb = new StringBuilder();

            foreach (var group in rtxt.Groups)
            {

                if (!string.IsNullOrEmpty(group.Section)) // no groups
                    sb.AppendLine($"[{group.Section}]");

                // Groups can contain zero entries
                foreach (var kvp in group.Entries.Where(kvp => kvp.Key != null || kvp.Value != null))
                {
                    // Append offsets only if they exist (they rarely do so we don't want to bloat the file)
                    var appendOffsets = kvp.OffsetX != 0 || kvp.OffsetY != 0 ? $"[x={kvp.OffsetX},y={kvp.OffsetY}]" : string.Empty;

                    // Handle case where key is null but value is empty string in a group (should be treated as empty key)
                    /*
                    if (!string.IsNullOrEmpty(group.Section) && kvp.Value == string.Empty && kvp.Key == null)
                  
                        kvp.Key = string.Empty;
                    
                    sb.AppendLine(kvp.Key == null
                        ? $"\"{kvp.Value.EscapeQuotes()}\";"
                        : $"\"{kvp.Key.EscapeQuotes()}\" = \"{appendOffsets}{kvp.Value.EscapeQuotes()}\";");
                    */

                    var emitKey = (!string.IsNullOrEmpty(group.Section) && kvp.Value == string.Empty && kvp.Key == null)
                        ? string.Empty
                        : kvp.Key;

                    sb.AppendLine(emitKey == null
                        ? $"\"{kvp.Value.EscapeQuotes()}\";"
                        : $"\"{emitKey.EscapeQuotes()}\" = \"{appendOffsets}{kvp.Value.EscapeQuotes()}\";");
                }

                sb.AppendLine();
            }

            // Replace null chars if needed
            return ValTableReadPastFirstNull
                ? sb.ToString().Replace("\0", ValTableNullReplaceWithChar.ToString())
                : sb.ToString();

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Is Valid Format
        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            if (!data.StartsWith(Def.MagicBytes))
                return false;

            // Group table offset and ref count must be non-negative
            var groupTableOffset = data.PeekInt32(4);
            var refTableCount = data.PeekInt32(12);

            if (groupTableOffset < 0 || refTableCount < 0)
                return false;

            // Group table offset should be within file bounds
            return groupTableOffset <= data.Length;
        }
        #endregion

    }

}