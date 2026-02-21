using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public enum FileType
    {
        Unknown = 0,
        TDI,
        TDO,
        BFC,
        BMS,
        BMP,
        CBIN,
        DDS,
        FNT,
        JPG,
        MP3,
        PAK,
        PCX,
        PNG,
        R16,
        RTXT,
        SCR,
        TGA,
        TXT,
        WAV,
    }
    public enum SerializeFormat
    {
        None,
        TXT,
        JSON,
        INI
    }

    ////////////////////////////////////////////////////////////////////////////////////
    #region Format Def Class
    public class FormatDef
    {
        public byte[] MagicBytes { get; set; }
        public string[] Extensions { get; set; }
        public string Notes { get; set; }
        // Validation delegate for formats that need additional checks
        public Func<string, byte[], bool> ValidatorDelegate { get; set; }
        // Image converter delegate for viewer
        public Func<byte[], Bitmap> ToBmpDelegate { get; set; } // bytes (Returns Bitmap)
        public ITextSerializer TextSerializer { get; set; }
        public SerializeFormat[] SerializeFormats { get; set; }

    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region Definitions Class
    public static class Definitions
    {
        private static readonly SerializeFormat[] EmptySerialize = Array.Empty<SerializeFormat>();

        private static readonly bool[] TxtForbiddenBytes = new bool[256];

        // Format registry
        private static readonly Dictionary<FileType, FormatDef> Registry = new Dictionary<FileType, FormatDef>();

        // Keys are masked uint values: 4-byte as-is, 3-byte & 0x00FFFFFF, 2-byte & 0x0000FFFF
        private static readonly Dictionary<uint, (FileType Type, FormatDef Def)> MagicLookup = new Dictionary<uint, (FileType, FormatDef)>();

        // Validator formats with no magic bytes
        private static readonly List<KeyValuePair<FileType, FormatDef>> ValidatorFormats = new List<KeyValuePair<FileType, FormatDef>>();

        // Exclusion signatures that immediately return FileType.Unknown so we don't try and inspect the full file
        private static readonly HashSet<uint> ExcludedMagic = new HashSet<uint>();
        private static bool _hasExcludedMagic;

        // Excluded file extensions that immediately return FileType.Unknown to skip full file inspection
        private static readonly HashSet<string> ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { 
            ".3DI", ".3DO", ".AIN", ".BAD", ".CPT", ".CR1", ".CR2", ".CRT", ".DBF", 
            ".LEV", ".LWF", ".MIB", ".OBJ", ".ORF", ".PIB", ".PRJ", ".PWF", 
            ".RAW", ".REF",  ".SAF" ,".SPX", ".TIL",
        };

        // Excluded file extensions specifically from IsTxt detection to avoid false positives
        private static readonly HashSet<string> ExcludedExtensionsTxt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {   // **** KEEP RAW EXTENSIONS HERE OR ADJUST ISTXT TO EXCLUDE IT ****
            ".RAW",
        };

        ////////////////////////////////////////////////////////////////////////////////////
        #region Notes
        private const string CbinNotes = @"Formatting (INI Mode):

- Groups / Sections:
  - Must be enclosed in brackets: [group_name]

- Key-Value Pairs:
  - Key-Value pairs must be under a [group] section.
  - It is NORMAL to see duplicated key-value pairs.
  - Enclose ONLY strings within VALUES with quotes
  - Ints and Floats must not be enclosed with quotes
  - Floats must have a decimal to be parsed as float

        [KEYS]
        KEY = ""EVNT_CLICK"",-0.0,0,255,-1,0,0,0,0,0,0,0         //""IT2_CLICK""
        KEY = ""EVNT_ICONCLICK"",1.9,0,255,-1,0,0,0,0,0,1,0     //""IT2_ICON_CLICK""
        KEY = ""EVNT_BIGDOORCLICK"",9.9,0,255,-1,0,0,0,0,0,2,0  //""IT2_CLICK_BIGDOOR""
        KEY = ""EVNT_EXITDOORCLICK"",-1.0,0,255,-1,0,0,0,0,0,3,0 //""IT2_CLICK_EXITDOOR""

        [FONTS]
        FONT = ""ARIAL12"",""image1.PCX"",""Gold"",""Black"",""LightGold"",""Black"",""DarkGold"",""Black""
        FONT = ""ARIAL12g"",""image2.PCX"",""Green"",""Green"",""LightGreen"",""Green"",""LightGreen"",""Green""
        FONT = ""ARIAL12p"",""image3.PCX"",""Purple"",""Purple"",""LightPurple"",""Purple"",""Purple"",""Purple""

- Strings:
  - Quotes within value strings must be escaped:

        KEY = ""Edge \""DJ\"" Case""

Failure to format the text correctly may cause issues with the game.

Please ensure you have a backup.
";

        private const string RTxtNotes = @"Formatting (INI Mode):

- Groups / Sections:
  - Must be enclosed in brackets: [group_name]

- Values Only:
  - Json.If the file only contains values, it cannot contain groups.
  - It is NORMAL to see duplicated lines.
  - Enclose each line in quotes, ending with a semicolon:

        ""String message 1"";
        ""String message 2"";

- Key-Value Pairs:
  - Key-Value pairs must be under a [group] section.
  - It is NORMAL to see duplicated key-value pairs.
  - Enclose both the key AND value in quotes, ending with a semicolon:

        [group1]
        ""key1"" = ""value1"";
        ""key2"" = ""value2"";

- Strings:
  - Quotes within value strings must be escaped:

        ""key1"" = ""Edge \""DJ\"" Case"";
        ""Edge \""DJ\"" Case"";

Failure to format the text correctly may cause issues with the game.

Please ensure you have a backup.";

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Format Definitions
        static Definitions()
        {
            // Register forbidden bytes for text detection
            RegisterForbiddenTxtBytes();

            // Define and register known formats
            Register(FileType.BFC,
                "BFC1".ToBytes(),
                new[] { ".dds", ".mdt", ".wav" }); //DFX2, JOCA, mdt=tga

            Register(FileType.BMS,
                "BMS".ToBytes(),
                new[] { ".bms" });

            Register(FileType.CBIN,
                "CBIN".ToBytes(),
                new[]
                {
                    ".anm", ".bas", ".bdf", ".box", ".cfg", ".def", ".des", ".hud", ".ics", ".itm", 
                    ".job", ".kda", ".mnu", ".mpc", ".nws", ".ocf", ".scr", ".sen", ".txt", ".wng"
                },
                notes: CbinNotes,
                serializer: new CBinSerializer(),
                serializeFormats: new[] { SerializeFormat.INI, SerializeFormat.JSON });

            Register(FileType.DDS,
                "DDS ".ToBytes(), // Note the space at the end
                new[] { ".dds" },
                toBmpDelegate:Dds.ToBmp);

            Register(FileType.FNT,
                "FNT0".ToBytes(), // Note the space at the end
                new[] { ".fnt" },
                toBmpDelegate: Fnt.ToBmp);

            Register(FileType.JPG,
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                new[] { ".jpg", ".jpeg" },
                toBmpDelegate: Jpg.ToBmp);

            Register(FileType.MP3,
                new byte[] { 0xFF, 0xF3, 0x60, 0xC4 },
                new[] { ".mp3" });

            Register(FileType.MP3,
                new byte[] { 0xFF, 0xFB, 0x30, 0xC4 },
                new[] { ".mp3" });

            Register(FileType.PAK,
                new byte[] { 0x33, 0x44, 0x50, 0x4B },
                new[] { ".pak" });

            Register(FileType.PCX,                           // PCX magic bytes inlude: identifier, version, encoding
                new byte[] { 0xA, 0x5, 0x1 },      // PCX does not have a static MagicBytes but all NovaLogic PCX start with (0x0A, 0x05, 0x01, 0x08 or 0x01)
                new[] { ".pcx", ".pal" },
                toBmpDelegate: Pcx.ToBmp);

            Register(FileType.PNG,                          
                new byte[] { 0x89, 0x50, 0x4E, 0x47 },
                new[] { ".png"},
                toBmpDelegate: Png.ToBmp);
            
            Register(FileType.R16,
                "R16a".ToBytes(),
                new[] { ".r16" },
                toBmpDelegate: R16.ToBmp);

            Register(FileType.RTXT,
                "RTXT".ToBytes(),
                new[] { ".bin" },
                notes: RTxtNotes,
                serializer: new RTxtSerializer(),
                serializeFormats: new[] { SerializeFormat.INI, SerializeFormat.JSON });

            Register(FileType.SCR,
                new byte[] { 0x53, 0x43, 0x52, 0x01 },
                new []{ ".aca", ".adm", ".aip", ".anm", ".bin", ".end", ".def", ".fx", ".ldo", ".csv", ".ptg", ".ptl", ".ptu", ".txt"},
                serializer: new ScrSerializer(),
                serializeFormats: new[] { SerializeFormat.TXT });

            Register(FileType.WAV,
                "RIFF".ToBytes(),
                new[] { ".wav" });

            // Work in Progress
            // DF1: 0x33, 0x44, 0x49, 0x05 (3DI-v)
            // DF2: 0x33, 0x44, 0x49, 0x08 (3DI-v)
            // LW/TFD: 0x33, 0x44, 0x49, 0x0A (3DI-v)
            // DFX/DFX2/JO: 0x33, 0x44, 0x49, 0x33 (3DI3)
            Register(FileType.TDI,
                new byte[] { 0x33, 0x44, 0x49}, 
                new[] { ".3di" });

            // BHD/TS/C4: 0x47, 0x50, 0x50, 0x02 (GPP-v)
            Register(FileType.TDI,
                new byte[] { 0x47, 0x50, 0x50 },
                new[] { ".3di" });

            // BHD/TS/C4: 0x47, 0x50, 0x4D, 0x02 (GPM-v)
            Register(FileType.TDI,
                new byte[] { 0x47, 0x50, 0x4D },
                new[] { ".3di" });

            Register(FileType.TDO,
                new byte[] { 0x33, 0x44, 0x4F, 0x31 },
                new[] { ".3do" });
   
            Register(FileType.TGA,
                new byte[] {},
                new[] { ".tga", ".mdt" },   
                validatorDelegate:Tga.Validator,
                toBmpDelegate:Tga.ToBmp);

            Register(FileType.TXT,
                new byte[] {},
                new[] { ".cfg", ".conf", ".env", ".grm", ".inf", ".ini", ".mnu", ".trn", ".txt" },
                validatorDelegate:(fileName, data) => Txt.IsTxt(fileName, data, TxtForbiddenBytes, ExcludedExtensionsTxt),
                serializer: new TxtSerializer(),
                serializeFormats: new[] { SerializeFormat.TXT });

            Register(FileType.BMP,
                "BM".ToBytes(),
                new[] { ".bmp" },
                validatorDelegate: Bmp.Validator,
                toBmpDelegate:Bmp.ToBmp);

            //ExcludeMagic("DUM".ToBytes());

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Registration
        private static void Register(
            FileType type, 
            byte[] magicBytes, 
            string[] extensions, 
            string notes = null,
            Func<string, byte[], bool> validatorDelegate = null,
            Func<byte[], Bitmap> toBmpDelegate = null,
            ITextSerializer serializer = null,
            SerializeFormat[] serializeFormats = null
            )
        {
            var def = new FormatDef
            {
                MagicBytes = magicBytes,
                Extensions = extensions,
                Notes = notes,
                ValidatorDelegate = validatorDelegate,
                ToBmpDelegate = toBmpDelegate,
                TextSerializer = serializer,
                SerializeFormats = serializeFormats ?? EmptySerialize
            };

            Registry[type] = def;

            // Insert into the magic lookup table based on magic byte length
            uint key;
            switch (magicBytes.Length)
            {
                case 0:
                    // validator only (e.g. TGA, TXT)
                    if (validatorDelegate != null)
                        ValidatorFormats.Add(new KeyValuePair<FileType, FormatDef>(type, def));
                    return;

                case 2:
                    key = (uint)(magicBytes[0] | (magicBytes[1] << 8));
                    break;

                case 3:
                    key = (uint)(magicBytes[0] | (magicBytes[1] << 8) | (magicBytes[2] << 16));
                    break;

                case 4:
                    key = BitConverter.ToUInt32(magicBytes, 0);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"{magicBytes.Length} byte magic not supported for '{type}'. Use a validator delegate instead.");

            }

            if (MagicLookup.TryGetValue(key, out var existing))
                throw new InvalidOperationException($"Duplicate magic bytes detected for '{type}' and '{existing.Type}'. Magic bytes must be unique.");

            MagicLookup.Add(key, (type, def));

        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static void RegisterForbiddenTxtBytes()
        {
            // ***DENY*** ASCII control characters 0x00-0x1F
            for (var i = 0; i < 32; i++)
                TxtForbiddenBytes[i] = true;

            // ***ALLOW*** common controls common in text files
            TxtForbiddenBytes[9] = false; // Tab
            TxtForbiddenBytes[10] = false; // Line Feed
            TxtForbiddenBytes[13] = false; // Carriage Return
            TxtForbiddenBytes[26] = false; // 0x1A (SUB) (Legacy EOF)

            // ***DENY*** Windows-1252 specific bytes. 
            TxtForbiddenBytes[0x81] = true;
            TxtForbiddenBytes[0x8D] = true;
            TxtForbiddenBytes[0x8F] = true;
            TxtForbiddenBytes[0x90] = true;
            TxtForbiddenBytes[0x9D] = true;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Helpers

        public static byte[] GetFormatMagicBytes(FileType type) => Registry.TryGetValue(type, out var value) ? value.MagicBytes : null;

        ////////////////////////////////////////////////////////////////////////////////////
        public static FormatDef GetFormatDef(FileType type)
        {
            return Registry.TryGetValue(type, out var value)
                ? value
                : throw new KeyNotFoundException($"No format definition registered for {type}.");
        }

        // Registers a 4-byte signature to immediately return Unknown, skipping any further checks.
        private static void ExcludeMagic(byte[] magicBytes)
        {
            if (magicBytes == null || magicBytes.Length < 2)
                throw new ArgumentException("Exclusion requires at least 2 magic bytes.");

            uint key;
            switch (magicBytes.Length)
            {
                case 2:
                    key = (uint)(magicBytes[0] | (magicBytes[1] << 8));
                    break;

                case 3:
                    key = (uint)(magicBytes[0] | (magicBytes[1] << 8) | (magicBytes[2] << 16));
                    break;

                case 4:
                    key = BitConverter.ToUInt32(magicBytes, 0);
                    break;

                default:
                    throw new ArgumentException($"{magicBytes.Length} byte exclusion is not supported.");
            }

            if (MagicLookup.TryGetValue(key, out var match))
                throw new InvalidOperationException($"Cannot exclude magic bytes that are already registered to '{match.Type}'.");

            ExcludedMagic.Add(key);
            _hasExcludedMagic = true;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Detection
        public static FileType DetectType(byte[] data, string fileName = null)
        {
            if (data == null || data.Length < 4)
                return FileType.Unknown;

            // Skip detection for known excluded extensions
            if (fileName != null)
            {
                var ext = Path.GetExtension(fileName);

                if (ext.Length > 0 && ExcludedExtensions.Contains(ext))
                    return FileType.Unknown;

                // DF1: ITEMS.DEF and LOOP.DEF have no magic bytes but are SCR
                if (fileName == "ITEMS.DEF" || fileName == "LOOP.DEF")
                    return FileType.SCR;
            }

            // Inline the uint read — data.Length >= 4 is already guaranteed above
            var magic = (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));

            // 4 byte
            if (_hasExcludedMagic && ExcludedMagic.Contains(magic))
                return FileType.Unknown;

            if (TryMatchMagic(data, magic, fileName, out var type))
                return type;

            // 3 byte
            var magic3 = magic & 0x00FFFFFF;
            if (_hasExcludedMagic && ExcludedMagic.Contains(magic3))
                return FileType.Unknown;

            if (TryMatchMagic(data, magic3, fileName, out type))
                return type;

            // 2 byte
            var magic2 = magic & 0x0000FFFF;
            if (_hasExcludedMagic && ExcludedMagic.Contains(magic2))
                return FileType.Unknown;

            if (TryMatchMagic(data, magic2, fileName, out type))
                return type;

            // Validator formats without magic bytes (TGA, TXT)
            foreach (var kvp in ValidatorFormats)
            {
                if (kvp.Value.ValidatorDelegate(fileName, data))
                    return kvp.Key;
            }

            #if DEBUG
            //DebugUnknownFormat(data, fileName);
            #endif

            return FileType.Unknown;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static bool TryMatchMagic(byte[] data, uint key, string fileName, out FileType type)
        {
            if (!MagicLookup.TryGetValue(key, out var match))
            {
                type = FileType.Unknown;
                return false;
            }

            type = match.Type;
            return match.Def.ValidatorDelegate == null || match.Def.ValidatorDelegate(fileName, data);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool IsType(string fileName, byte[] data, FileType type) => Registry.TryGetValue(type, out var def) && Validate(fileName, data, def);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Validation
        private static bool Validate(string fileName, byte[] data, FormatDef def)
        {
            if (def == null || data == null) 
                return false;

            if (data.Length < def.MagicBytes.Length) 
                return false;

            return def.ValidatorDelegate?.Invoke(fileName, data)
                   ?? data.StartsWith(def.MagicBytes);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Debug
        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugUnknownFormat(byte[] data, string fileName)
        {
            var peek = data.ReadBytes(0, Math.Min(4, data.Length));
            var magic = peek.IsPrintableAscii(charsOnly: true)
                ? $"\"{System.Text.Encoding.ASCII.GetString(peek)}\""
                : peek.ToHex(" ");

            System.Diagnostics.Debug.WriteLine($"[DetectType] Unknown: {magic,-14} File: {fileName ?? "(no name)"}");
        }
        #endregion
    }
    #endregion

}