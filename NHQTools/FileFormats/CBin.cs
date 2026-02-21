using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public static class CBin
    {
        // Public
        public const int HeaderLen = 20;
        public const int MinExpectedLen = HeaderLen + 4; // +4 for group count which should always exist
        public const uint DefaultEncryptionKey = 0x01E177CE; // Seems to always be the same, across all games

        // Constants
        private const int SZ_GROUP_HEADER = 8;
        private const int SZ_ENTRY_TABLE = 8;

        // Regex Filters
        private static readonly Regex RxGroupNameFilter = new Regex("[^a-z0-9_]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RxKeyFilter = new Regex("[^a-z0-9/_: ]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex Matches
        private static readonly Regex RxKeyValPair = new Regex("^([^=]+)=(.*)$", RegexOptions.Compiled);
        private static readonly Regex RxValueToken = new Regex(@"(""(?:\\.|[^""\\])*"")|([^,]+)", RegexOptions.Compiled);

        private static readonly Regex RxFindKey = new Regex(
            "^// ?KEY:0x([0-9a-f]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Leave \A to ensure we only strip a key comment at the very beginning of the file
        // So when we change this back to multi-line mode, we don't wonder what happened 
        private static readonly Regex RxStripKey = new Regex(
            @"\A// ?KEY:0x[0-9a-f]+[^\S\r\n]*(?:\r\n|\r|\n)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.GetEncoding(Common.NlCodepage);

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static CBin() => Def = Definitions.GetFormatDef(FileType.CBIN);

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

            var cbin = Decrypt(fileData, enc);

            return SerializeToText(cbin, format);
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

        ////////////////////////////////////////////////////////////////////////////////////
        public static byte[] FromTxt(string textData, SerializeFormat format, Encoding enc = null)
        {
            enc = enc ?? DefaultEnc;

            if (string.IsNullOrEmpty(textData))
                throw new ArgumentException("Text cannot be empty.", nameof(textData));

            if (!Def.SerializeFormats.Contains(format))
                throw new ArgumentException("Unsupported serialization format.", nameof(format));

            var cbin = ParseText(textData, format, enc);

            return Encrypt(cbin);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Decrypt
        private static CBinFile Decrypt(byte[] data, Encoding enc)
        {
            // Clone to avoid mutating original. XorCipher decrypts in-place
            var reader = new ByteReader((byte[])data.Clone(), enc);

            // Header
            var magic = reader.ReadBytes(Def.MagicBytes.Length);
            var stringTableOffset = reader.ReadUInt32();
            var stringTableLength = reader.ReadUInt32();
            var stringTableCount = reader.ReadUInt32();
            var encryptionKey = reader.ReadUInt32();

            // Verify Magic Bytes
            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            // Validate Header fields
            if (stringTableOffset == 0 || stringTableLength == 0 || stringTableCount == 0)
                throw new InvalidDataException("The file is corrupt or an unsupported format (invalid string table).");

            if (stringTableOffset + stringTableLength > reader.Length)
                throw new InvalidDataException("The file is corrupt or an unsupported format (string table exceeds file size).");

            if (encryptionKey == 0)
                throw new InvalidDataException("The file is corrupt or an unsupported format (invalid encryption key).");

            // Decrypt Payload
            var payloadLength = reader.Length - HeaderLen;
            XorCipher(reader.Data, HeaderLen, payloadLength, encryptionKey);

            // Read String Table
            reader.Seek(stringTableOffset);
            var stringTable = new StringTable(enc);

            // CBIN uses 1-based indexes
            for (var i = 1; i <= stringTableCount; i++)
                stringTable.Add(reader.ReadCString());

            // Read Groups
            reader.Seek(HeaderLen);

            var groupCount = reader.ReadUInt32();

            var cbin = new CBinFile
            {
                Enc = enc,
                EncryptionKey = encryptionKey
            };

            // Temp list to hold metadata needed for the next steps
            // Item1: CBinGroup, Item2: EntryCount
            var tempGroups = new List<Tuple<CBinGroup, uint>>();

            for (var i = 0; i < groupCount; i++)
            {
                var stringId = reader.ReadUInt32();
                var entryCount = reader.ReadUInt32();

                var group = new CBinGroup(stringTable.Get(stringId));

                cbin.Groups.Add(group);

                tempGroups.Add(new Tuple<CBinGroup, uint>(group, entryCount));
            }

            // Read Keys...
            // Keys are stored sequentially for each group that has keys.
            // If EntryCount > 0, we read that many keys, then skip 8 bytes padding.
            foreach (var (group, entryCount) in tempGroups)
            {
                if (entryCount == 0)
                    continue;

                for (var i = 0; i < entryCount; i++)
                {
                    var keyStringId = reader.ReadUInt32();
                    var valCount = reader.ReadUInt32();

                    var key = new CBinKey(stringTable.Get(keyStringId));
                    group.Entries.Add(key);

                    // Allocate placeholders for values so we can fill them in the next pass
                    for (var v = 0; v < valCount; v++)
                        key.Values.Add(null);
                }

                reader.Skip(SZ_ENTRY_TABLE); // Skip padding
            }

            // Read Values...
            // Values are stored sequentially for EVERY key in EVERY group.
            foreach (var key in cbin.Groups.SelectMany(group => group.Entries))
            {
                // Fill the placeholders created above
                for (var i = 0; i < key.Values.Count; i++)
                {
                    var val = reader.ReadUInt32();
                    var type = (CBinValueType)reader.ReadInt32();

                    string valStr;

                    switch (type)
                    {
                        case CBinValueType.Int:
                            valStr = ((int)val).ToString(CultureInfo.InvariantCulture);
                            break;
                        case CBinValueType.Float:
                            valStr = FormatFloat(val);
                            break;
                        case CBinValueType.String:
                            valStr = stringTable.Get(val);
                            break;
                        default:
                            valStr = val.ToString();
                            break;
                    }

                    key.Values[i] = new CBinValue(type, valStr);
                }

            }

            return cbin;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Encrypt
        private static byte[] Encrypt(CBinFile cbin)
        {
            var enc = cbin.Enc;
            var groups = cbin.Groups;

            // Rebuild String Table in the exact order of:
            // Group Names > Key Names > Value Strings.
            var stringTable = new StringTable(enc);

            // Group Names
            foreach (var g in groups)
                stringTable.GetOrAdd(g.Section);

            // Key Names
            foreach (var k in groups.SelectMany(g => g.Entries))
                stringTable.GetOrAdd(k.Key);

            // Value Strings
            foreach (var v in groups.SelectMany(g => g.Entries.SelectMany(k => k.Values.Where(v => v.Type == CBinValueType.String))))
                stringTable.GetOrAdd(v.Value);

            // Calculate Sizes
            var groupCount = groups.Count;
            var groupTableLen = groupCount * SZ_GROUP_HEADER; // + 4 bytes for count (written separately)

            var totalKeys = groups.Sum(g => g.Entries.Count);
            var groupsWithKeys = groups.Count(g => g.Entries.Count > 0);
            var keyTableLen = (totalKeys * SZ_ENTRY_TABLE) + (groupsWithKeys * SZ_ENTRY_TABLE);

            var totalValues = groups.Sum(g => g.Entries.Sum(k => k.Values.Count));
            var valueTableLen = totalValues * SZ_ENTRY_TABLE;

            var strTableBytes = stringTable.GetAllBytes().ToList();
            var strTableLen = strTableBytes.Sum(b => b.Length);

            // Offset calculation
            var strTableOffset = HeaderLen + 4 + groupTableLen + keyTableLen + valueTableLen;
            var totalSize = strTableOffset + strTableLen;

            // Write Data
            var writer = new ByteWriter(totalSize, enc);

            // Header
            writer.Write(Def.MagicBytes);
            writer.Write((uint)strTableOffset);
            writer.Write((uint)strTableLen);
            writer.Write((uint)stringTable.Count);
            writer.Write(cbin.EncryptionKey);

            // Group Count
            writer.Write(groupCount);

            // Group Table
            foreach (var g in groups)
            {
                writer.Write(stringTable.GetOrAdd(g.Section));
                writer.Write(g.Entries.Count);
            }

            // Key Table
            foreach (var g in groups.Where(g => g.Entries.Count != 0))
            {
                foreach (var k in g.Entries)
                {
                    writer.Write(stringTable.GetOrAdd(k.Key));
                    writer.Write(k.Values.Count);
                }
                writer.Write(new byte[SZ_ENTRY_TABLE]); // Padding
            }

            // Value Table
            foreach (var v in groups.SelectMany(g => g.Entries.SelectMany(k => k.Values)))
            {
                switch (v.Type)
                {
                    case CBinValueType.String:
                        writer.Write(stringTable.GetOrAdd(v.Value));
                        break;
                    case CBinValueType.Float:
                        writer.Write(ParseFloat(v.Value));
                        break;
                    default:
                        int.TryParse(v.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iVal);
                        writer.Write((uint)iVal);
                        break;
                }

                writer.Write((int)v.Type);
            }

            // String Table
            foreach (var b in strTableBytes)
                writer.Write(b);

            // Encrypt Payload
            XorCipher(writer.Data, HeaderLen, writer.Length - HeaderLen, cbin.EncryptionKey);

            return writer.ToArray();
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Cipher
        ////////////////////////////////////////////////////////////////////////////////////
        public static void XorCipher(byte[] buffer, int pos, int length, uint key)
        {
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("Data cannot be null or empty.", nameof(buffer));

            if (pos < 0 || length < 0 || (long)pos + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(pos), "Index out of range");

            var end = pos + length;

            // Rotates the key left by 7 bits every byte
            for (var i = pos; i < end; i++)
            {
                key = key.RotateLeft(7);
                buffer[i] ^= (byte)key;
            }

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Parse
        private static CBinFile ParseText(string textData, SerializeFormat format, Encoding enc) =>
            format == SerializeFormat.JSON ? ParseJson(textData, enc) : ParseIni(textData, enc);

        ////////////////////////////////////////////////////////////////////////////////////
        private static CBinFile ParseJson(string textData, Encoding enc)
        {
            if (string.IsNullOrEmpty(textData))
                throw new ArgumentException("Text cannot be empty.", nameof(textData));

            // Deserialize
            var cbin = textData.Parse<CBinFile>();

            if (cbin == null)
                throw new InvalidDataException("Failed to parse JSON.");

            // Restore Encoding
            cbin.Enc = enc;

            return cbin;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static CBinFile ParseIni(string textData, Encoding enc)
        {
            if (string.IsNullOrEmpty(textData))
                throw new ArgumentException("Text cannot be empty.", nameof(textData));

            // Strip nulls from text to stop our regexes from breaking
            textData = textData.Replace("\0", "");

            // Match key comment
            var keyMatch = FindKey(textData);

            var encryptionKey = keyMatch.Success
                ? Convert.ToUInt32(keyMatch.Groups[1].Value, 16)
                : DefaultEncryptionKey;

            // Remove key comment from text
            textData = StripKey(textData);

            // Split and flatten lines
            var lines = textData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ln => ln.Trim())
                .Where(ln => ln.Length > 0)
                //.Where(ln => !ln.StartsWith("//KEY:0x") && !ln.StartsWith("// KEY:0x")) // Skip key comment line
                .ToList();

            // Parse matches into groups and entries
            var groups = new List<CBinGroup>();
            CBinGroup group = null;

            // Read Lines
            foreach (var line in lines)
            {
                // [GROUP] or [SECTION]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    // Strip [ and ] from group name
                    var name = line.Substring(1, line.Length - 2);

                    var cleanName = RxGroupNameFilter.Replace(name, string.Empty);

                    if (cleanName.Length == 0)
                        throw new InvalidDataException($"Group name '{name}' contains invalid chars. Only A-Z, 0-9, and _ are allowed.");

                    group = new CBinGroup { Section = cleanName };

                    groups.Add(group);

                    continue;
                }

                // Don't allow key-value pairs outside of groups
                if (group == null)
                    throw new InvalidDataException($"Key-value pairs '{line}' are not allowed outside of groups.");

                // Key-Value Pairs
                var kvpMatches = RxKeyValPair.Match(line);

                if (!kvpMatches.Success)
                    throw new InvalidDataException($"Invalid key-value pair format: '{line}'. Expected format 'key = value'.");

                var rawKey = kvpMatches.Groups[1].Value.Trim();
                var rawVal = kvpMatches.Groups[2].Value.Trim();

                //Fix leading comments (////Comment > //Comment)
                rawKey = Regex.Replace(rawKey, "^/+", "//");

                // Remove illegal chars except (a-z0-9, _, /, :, and space only)
                var cleanKey = RxKeyFilter.Replace(rawKey, string.Empty);

                // Convert leading/trailing slashes to strict ,//, so they are treated as separate tokens
                var cleanVal = rawVal.Replace("//", ",//,");

                var tokenMatches = RxValueToken.Matches(cleanVal)
                    .Cast<Match>()
                    .Select(m => m.Value.Trim())
                    .Where(v => v.Length > 0)
                    .ToList();

                // Create RefKey
                var refKey = new CBinKey { Key = cleanKey }; // *** DO NOT TRIM ***

                // Parse Values (and find more edge cases)
                foreach (var token in tokenMatches)
                {
                    var refVal = new CBinValue();

                    // String
                    if (token.StartsWith("\"") && token.EndsWith("\""))
                    {
                        refVal.Type = CBinValueType.String;

                        // Remove outer quotes and unescape inner quotes
                        var innerString = token.Substring(1, token.Length - 2)
                            .Replace("\\\"", "\""); // *** DO NOT TRIM ***

                        refVal.Value = innerString;
                    }

                    // Comment (Treated a string)
                    else if (token == "//")
                    {
                        refVal.Type = CBinValueType.String;
                        refVal.Value = token;
                    }

                    /* **** LEAVE THIS HERE TO REMEMBER THE HOURS WASTED ****
                    else if (token.Contains(".") && float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var fValTest))
                    {
                        refVal.Type = CBinValueType.Float;
                        refVal.Value = BitConverter.ToUInt32(BitConverter.GetBytes(fValTest), 0);
                    }
                    */

                    // Float
                    else if (token.TryParseFloat(NumberStyles.Float, CultureInfo.InvariantCulture, out uint fVal))
                    {
                        refVal.Type = CBinValueType.Float;
                        refVal.Value = FormatFloat(fVal);

                        if (token == "-0.0") // refVal.Value should return 0x80000000 for -0.0
                            Debug.WriteLine($"token: {token,-7} fVal: 0x{fVal,-10:X} refVal: 0x{ParseFloat(refVal.Value),-10:X}");

                    }

                    // Int
                    else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iVal))
                    {
                        refVal.Type = CBinValueType.Int;
                        refVal.Value = iVal.ToString();
                    }

                    // Something broken
                    else
                    {
                        throw new InvalidDataException($"Value '{token}' in key '{cleanKey}' is invalid. Strings must be enclosed in \"quotes\".");
                    }

                    refKey.Values.Add(refVal);
                }

                group.Entries.Add(refKey);
            }

            // Build CBinFile
            return new CBinFile
            {
                Enc = enc,
                EncryptionKey = encryptionKey,
                Groups = groups
            };

        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Formatting

        private static string SerializeToText(CBinFile cbinObj, SerializeFormat format)
        {

            // JSON 
            if (format == SerializeFormat.JSON)
                return cbinObj.Stringify(prettyPrint: true);

            // INI 
            var sb = new StringBuilder();

            // Include Encryption Key as comment
            sb.AppendLine(PrependKey(string.Empty, cbinObj.EncryptionKey));
            sb.AppendLine(); // Blank line after key for readability

            foreach (var group in cbinObj.Groups)
            {
                sb.AppendLine($"[{group.Section}]");

                foreach (var key in group.Entries)
                    sb.AppendLine($"{key.Key} = {key.ValuesFormatted}");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static string FormatFloat(uint floatVal)
        {
            if (floatVal == 0x80000000)
                return "-0.0";

            var bytes = BitConverter.GetBytes(floatVal);
            var fVal = BitConverter.ToSingle(bytes, 0);

            if (float.IsNaN(fVal))
                return "NaN";

            if (float.IsPositiveInfinity(fVal))
                return "Infinity";

            if (float.IsNegativeInfinity(fVal))
                return "-Infinity";

            var fStr = fVal.ToString("R", CultureInfo.InvariantCulture);

            if (!fStr.Contains(".") && !fStr.Contains("E") && !fStr.Contains("e"))
                return fStr + ".0";

            return fStr;
        }

        private static uint ParseFloat(string strVal)
        {
            switch (strVal)
            {
                case "-0.0":
                    return 0x80000000;
                case "NaN":
                    return 0xFFC00000;
                case "Infinity":
                    return 0x7F800000;
                case "-Infinity":
                    return 0xFF800000;
            }

            return float.TryParse(strVal, NumberStyles.Float, CultureInfo.InvariantCulture, out var fVal)
                ? BitConverter.ToUInt32(BitConverter.GetBytes(fVal), 0)
                : 0;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Key Comment Helpers

        private static string PrependKey(string text, uint key)
            => $"// KEY:0x{key:X8}{Environment.NewLine}{text}";

        private static Match FindKey(string text) => RxFindKey.Match(text);

        private static string StripKey(string text) => RxStripKey.Replace(text, "");

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region String Table Helper Class
        private class StringTable
        {
            private readonly Encoding _enc;
            private readonly List<string> _strings = new List<string>();
            private readonly Dictionary<string, uint> _lookup = new Dictionary<string, uint>();

            public int Count => _strings.Count;

            public StringTable(Encoding enc) => _enc = enc;

            public void Add(string s) => _strings.Add(s);

            public string Get(uint id) => (id > 0 && id <= _strings.Count) ? _strings[(int)id - 1] : "";

            public uint GetOrAdd(string s)
            {
                s = s ?? string.Empty;

                if (_lookup.TryGetValue(s, out var id))
                    return id;

                _strings.Add(s);

                id = (uint)_strings.Count;

                _lookup[s] = id;
                return id;
            }

            public IEnumerable<byte[]> GetAllBytes() =>
                _strings.Select(s => _enc.GetBytes(s).Concat(new byte[] { 0 }).ToArray());

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

            // String table offset + length should not exceed file size
            var strTableOffset = data.PeekUInt32(4);
            var strTableLength = data.PeekUInt32(8);
            if (strTableOffset + strTableLength > (uint)data.Length)
                return false;

            // Encryption key must be non-zero
            var encryptionKey = data.PeekUInt32(16);
            return encryptionKey != 0;
        }
        #endregion

    }

}