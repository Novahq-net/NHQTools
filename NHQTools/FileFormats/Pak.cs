using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    ////////////////////////////////////////////////////////////////////////////////////
    // PAK files are 3D model containers used by older NovaLogic games. Each PAK contains
    // one or more 3DO model chunks followed by a texture table with RLE-encoded PCX
    // textures. Textures may have an embedded 768-byte palette, or rely on an external
    // .PAL file (found in the PFF alongside the PAK) for palette data.
    //
    // The extracted 3DO model chunks from this tool may or may not be valid 3DO files.
    // Some 3DO files appear to have embedded textures or other data I haven't spent
    // a lot of time trying to figure out. These are mostly for future reference:
    //
    // These files contain data with no recognizable headers or signatures:
    // Failed to process MGATCLSD.PAK: No 3DO chunks found.
    // Failed to process MGATOPEN.PAK: No 3DO chunks found.
    // Failed to process SGATCLSD.PAK: No 3DO chunks found.
    // Failed to process SGATOPEN.PAK: No 3DO chunks found.
    //
    // Some PAK files contain other texture formats, but for now only PCX is supported.
    //
    // The debug logging is kind of a mess. I'm leaving it in place for now for when/if
    // I ever decide to decipher the other texture formats.
    ////////////////////////////////////////////////////////////////////////////////////
    public static class Pak
    {
        // Header
        public const int HeaderLen = 4; // "3DPK" (4)
        public const int MinExpectedLen = HeaderLen + 60; // At least up to texture table offset

        // Texture table
        public const int TextureTableOffsetPtr = 60;
        public const int TextureTableEndOffsetPtr = 64; // Should lead to EOF

        // Entry sizes
        public const int ModelNameLen = 8;
        public const int TextureNameLen = 11;

        // Palette
        public const ushort Palette256 = 256; // Texture has embedded palette with 256 colors

        ////////////////////////////////////////////////////////////////////////////////////
        public static readonly Encoding DefaultEnc = Encoding.ASCII;

        private static readonly byte[] _3DO1Bytes = DefaultEnc.GetBytes("3DO1");

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly FormatDef Def;

        ////////////////////////////////////////////////////////////////////////////////////
        static Pak() => Def = Definitions.GetFormatDef(FileType.PAK);

        ////////////////////////////////////////////////////////////////////////////////////
        #region Unpack

        public static Dictionary<string, byte[]> Unpack(FileInfo file, byte[] extPalette = null, bool saveAlphaPng = false)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "File cannot be null.");

            return !file.Exists
                ? throw new FileNotFoundException($"File '{file.FullName}' not found.", file.FullName)
                : Unpack(file.Name, File.ReadAllBytes(file.FullName), extPalette, saveAlphaPng);
        }

        public static Dictionary<string, byte[]> Unpack(string fileName, byte[] fileData, byte[] extPalette = null, bool saveAlphaPng = false)
        {
            if (fileData == null || fileData.Length < MinExpectedLen)
                throw new InvalidDataException("Data is empty or too short.");

            var reader = new ByteReader(fileData);

            var magic = reader.ReadBytes(Def.MagicBytes.Length);

            if (!magic.Matches(Def.MagicBytes))
                throw new InvalidDataException($"Invalid file signature. Expected '{Def.MagicBytes.AsString()}', got '{magic.AsString()}'");

            var textureTableOffset = reader.PeekInt32(TextureTableOffsetPtr);
            var textureTableEnd = reader.PeekInt32(TextureTableEndOffsetPtr);

            if (textureTableOffset > fileData.Length)
                throw new InvalidDataException("File data is smaller than expected texture table start.");

            if (fileData.Length < textureTableEnd)
                throw new InvalidDataException("File data is smaller than expected texture table end.");

            var modelMatches = fileData.FindMatches(_3DO1Bytes, MinExpectedLen, textureTableOffset);

            if (modelMatches.Count == 0)
                throw new InvalidDataException("No 3DO chunks found.");

            // Output files / logging
            var outFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var log = new StringBuilder();

            // Model chunks
            reader.Seek(modelMatches[0]);

            AddLog(log, new string('-', 50));
            AddLog(log, $"Found {modelMatches.Count} model chunks.");

            for (var i = 0; i < modelMatches.Count; i++)
            {
                var modelStart = modelMatches[i];
                var modelEnd = (i + 1 < modelMatches.Count) ? modelMatches[i + 1] : textureTableOffset;

                var model = ReadModelEntry(reader, modelEnd - modelStart);

                AddFile(log, i, outFiles, model.FileName, model.Data);
            }

            // External palette
            var palName = string.Empty;

            if (extPalette != null)
            {
                palName = Path.GetFileNameWithoutExtension(fileName) + ".PAL";
                AddLog(log, new string('-', 50));
                AddLog(log, $"Found {palName} external palette.");
                AddFile(log, -1, outFiles, palName, extPalette);
            }

            // Texture chunks
            reader.Seek(textureTableOffset);

            var textureCount = reader.ReadInt32();

            AddLog(log, new string('-', 50));
            AddLog(log, $"Found {textureCount} texture chunks.");

            for (var i = 0; i < textureCount; i++)
            {
                var texture = ReadTextureEntry(reader);
                var paletteUsed = texture.HasPalette ? "embedded palette" : (extPalette != null ? palName : "gray palette");

                var width = (int)texture.Width;
                var height = (int)texture.Height;

                var pcx = BuildPcx(texture.Rle, width, height, texture.Palette ?? extPalette);

                AddFile(log, i, outFiles, texture.FileName, pcx, paletteUsed);

                if(saveAlphaPng) {

                    var pngName = Path.GetFileNameWithoutExtension(texture.FileName) + ".PNG";
                    var png = Pcx.ToPng(pcx, true);
                    AddFile(log, -1, outFiles, pngName, png);
                }

            }

            AddFile(log, -1, outFiles, "_" + Path.GetFileNameWithoutExtension(fileName) + "_EXPORTS.TXT", DefaultEnc.GetBytes(log.ToString()));

            return outFiles;
        }

        private static void AddFile(StringBuilder log, int index, Dictionary<string, byte[]> outFiles, string name, byte[] data, string paletteInfo = null)
        {

            var prefix = index == -1 ? $"{"",4}" : $"{(index + 1),3}:";

            if (outFiles.TryGetValue(name, out var existingBytes))
            {
                // Some chunks have duplicate model names with the exact same data. Not sure why.
                var suffix = existingBytes.SequenceEqual(data) ? "duplicate, skipped" : "duplicate with diff bytes?";
                AddLog(log, $"  {prefix} {name,-15} ({suffix})");
                return;
            }

            AddLog(log,
                string.IsNullOrEmpty(paletteInfo)
                    ? $"  {prefix} {name}"
                    : $"  {prefix} {name,-15} ({paletteInfo})");

            outFiles.Add(name, data);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Model Extraction

        private static PakModelEntry ReadModelEntry(ByteReader reader, int modelLen)
        {
            var modelChunk = reader.ReadBytes(modelLen);
            var nameBytes = modelChunk.ReadBytes(8, ModelNameLen);

            return new PakModelEntry
            {
                Name = nameBytes,
                Data = modelChunk
            };
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Texture Extraction

        private static PakTextureEntry ReadTextureEntry(ByteReader reader)
        {
            var entryHeaderBytes = reader.PeekBytes(reader.Position, 28);

            // Fixed 11 bytes, null-padded
            // ReadCString does not work because the 12th byte is NOT a null terminator
            // Read notes in PakFile.cs
            var nameBytes = reader.ReadBytes(TextureNameLen);

            _ = reader.ReadByte();              // unk1
            var paletteCount = reader.ReadUInt16();
            _ = reader.ReadByte();              // unk2
            _ = reader.ReadByte();              // unk3

            var width = reader.ReadUInt32();
            var height = reader.ReadUInt32();
            var dataLen = reader.ReadInt32();

            var rle = reader.ReadBytes(dataLen);

            byte[] palette = null;
            if (paletteCount == Palette256)
                palette = reader.ReadBytes(Pcx.PaletteLen);

            return new PakTextureEntry
            {
                Name = nameBytes,
                PaletteCount = paletteCount,
                Width = width,
                Height = height,
                DataLen = dataLen,
                Rle = rle,
                Palette = palette,
                HeaderBytes = entryHeaderBytes
            };
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region PCX Assembly

        private static byte[] BuildPcx(byte[] rleData, int width, int height, byte[] palette = null)
        {
            if (rleData == null || rleData.Length == 0)
                throw new InvalidDataException("RLE data is empty.");

            palette = ResolvePalette(ref rleData, palette);

            var header = Pcx.CreateHeader(width, height, Pcx.ColorMode.Idx8);

            var writer = new ByteWriter(header.Length + rleData.Length + 1 + Pcx.PaletteLen, DefaultEnc);

            writer.Write(header);
            writer.Write(rleData);
            writer.Write((byte)Pcx.PaletteMarker);
            writer.Write(palette);

            return writer.ToArray();
        }

        // Resolves a palette from available sources, stripping embedded palette
        // data from rleData if found. Looks for embedded palette first, then
        // external palette, then falls back to gray palette.
        private static byte[] ResolvePalette(ref byte[] rleData, byte[] palette)
        {
            // Check for embedded palette within RLE data (0x0C marker + 768 palette bytes)
            // Must check BEFORE stripping dangling marker, the last palette byte could be 0x0C
            if (Pcx.TryReadPalette(rleData, out var embedded))
            {
                rleData = rleData.ReadBytes(0, rleData.Length - (Pcx.PaletteLen + 1));
                return embedded;
            }

            // Strip any dangling palette marker (0x0C as last byte with no palette following)
            if (rleData.Length > 0 && rleData[rleData.Length - 1] == Pcx.PaletteMarker)
                rleData = rleData.ReadBytes(0, rleData.Length - 1);

            if (palette == null)
                return Pcx.CreateGrayPalette();

            // Already a clean palette
            if (palette.Length == Pcx.PaletteLen)
                return palette;

            // Extract the trailing palette from a .PAL file
            Pcx.TryReadPalette(palette, out var extracted);

            return extracted ?? Pcx.CreateGrayPalette();
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Logging

        private static void AddLog(StringBuilder log, string line)
        {
            Debug.WriteLine(line);
            log.AppendLine(line);
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Validation

        internal static bool Validator(string fileName, byte[] data)
        {
            if (data == null || data.Length < MinExpectedLen)
                return false;

            if (!data.StartsWith(Def.MagicBytes))
                return false;

            var textureTableOffset = data.PeekInt32(TextureTableOffsetPtr);
            return textureTableOffset > 0 && textureTableOffset < data.Length;
        }

        #endregion

    }

}