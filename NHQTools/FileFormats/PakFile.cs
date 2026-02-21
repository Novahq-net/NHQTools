using System;

namespace NHQTools.FileFormats
{

    ////////////////////////////////////////////////////////////////////////////////////
    // ModelEntry structure (Not fully understood):
    public class PakModelEntry
    {
        public byte[] Name { get; set; }
        public byte[] Data { get; set; }
        public string BaseName => Pak.DefaultEnc.GetString(Name).Trim();
        public string FileNameExt => ".3DO";
        public string FileName => BaseName + FileNameExt;
        
    }

    ////////////////////////////////////////////////////////////////////////////////////
    // TextureEntry structure (28 bytes):
    //   Name[11], AlphaKeyIndex[1], PaletteCount[2], Flags1[1], Flags2[1], Width[4], Height[4], DataLen[4]
    public class PakTextureEntry
    {
        // Texture name is fixed 11 bytes, null/space-padded. 
        // The name entry length can be misleading when looking at a hex
        // editor. Some files appear to have a 12 byte name field because
        // it ends in X, so it looks like 11_CHARS.PCX but the X is actually
        // the first byte of the Meta fields. It just happens it
        // decodes to ASCII 0x58 'X'.
        public byte[] Name { get; set; }

        // Meta
        //public byte Unk1;                         // unknown
        public ushort PaletteCount { get; set; }    // 0 for no palette, 256 for 768-byte palette (RBG*256)
        //public byte Unk2;                         // unknown
        //public byte Unk3;                         // unknown

        public uint Width { get; set; }
        public uint Height { get; set; }
        public int DataLen { get; set; }    // length of RLE blob (**DOES NOT include palette**)
        public byte[] Rle { get; set; }     // (***INCLUDES*** 0x0C palette marker)
        public byte[] Palette { get; set; } // 768 bytes when PaletteCount == 256

        public bool HasPalette => PaletteCount == Pak.Palette256 && Palette != null && Palette.Length == Pcx.PaletteLen;

        public byte[] HeaderBytes;

        public string BaseName
        {
            get
            {
                var extIndex = Array.IndexOf(Name, (byte)'.');
                return (extIndex >= 0) ? Pak.DefaultEnc.GetString(Name, 0, extIndex) : "texture";       
            }
        }

        public string FileName => BaseName + ".PCX";
    }


}