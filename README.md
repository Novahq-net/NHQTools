## NovaHQ Tools Library for NovaLogic Games

Visit [https://novahq.net](https://novahq.net) for more information on NovaLogic file formats and tools to work with these files. 

**Related research notes**:

- [NovaLogic File Formats](https://github.com/Novahq-net/NovaResearch/)

## NHQTools Library

A .NET Framework 4.8 class library mainly used for handling, parsing, and serializing various file formats used in NovaLogic games. The library provides a unified API for working with these formats, including auto-detection of file types and support for both text-based and binary formats.

### Supported Formats
The library includes a format registry (`Definitions` / `FormatDef`) and specialized serializers for:
* **Text/Data**: `.BIN`, `.DEF`, `.MNU`, and others via `Scr`, `RTxt`, `CBin`, and `Txt` serializers.
* **Images**: `.PCX`, `.DDS`, `.R16`, `.TGA`, `.FNT`
* **Containers**: `.PFF`, `.PAK`, `.BFC`
* **Audio**: `.WAV`

---

## Repository Layout
* `NHQTools/` – The core class library with all parsing and serialization logic.
* `NHQTools/FileFormats/` – Core file format definitions and serializers for various NovaLogic formats.
* `NHQTools/*/` – Shared library code, utilities, and common logic for both the library and GUI apps.

---

### Tools using `NHQTools.dll`
- [NovaPff](https://github.com/Novahq-net/NovaPff/)

---

### Using `NHQTools.dll` in your own project
Reference the compiled `NHQTools.dll` and import the `NHQTools.FileFormats` namespace.
I plan to refactor the format definitions and serializers into more modular components,
but for now you can directly use the `Definitions` class to detect formats and access their serializers.

**Auto-detect and decrypt an items.def (SCR) file to plain text:**
```csharp
using NHQTools.FileFormats;

var fileBytes = File.ReadAllBytes("items.def");
var fileType = Definitions.DetectType(fileBytes);
var formatDef = Definitions.GetFormatDef(fileType);

if (formatDef.TextSerializer == null) 
    throw new NotSupportedException($"Format {fileType} does not have a text serializer.");

var txt = formatDef.TextSerializer.ToTxt(fileBytes, SerializeFormat.INI);
File.WriteAllText("items.def.txt", txt);

// To encrypt it back, just reverse the process:
var txtData = File.ReadAllText("items.def.txt");
var formatDef = Definitions.GetFormatDef(fileType);

if (formatDef.TextSerializer == null) 
    throw new NotSupportedException($"Format {fileType} does not have a text serializer.");

var bytes = formatDef.TextSerializer.FromTxt(txtData, SerializeFormat.INI);
File.WriteAllBytes("items.def", bytes);

```
**Manually specify FileType.SCR to decrypt/encrypt an items.def (SCR) file to plain text:**
```csharp
using NHQTools.FileFormats;

// Decrypt
var fileBytes = File.ReadAllBytes("items.def");
var txt = Scr.ToTxt(fileBytes, SerializeFormat.INI);
File.WriteAllText("items.def.txt", txt);

// Encrypt
var txt = File.ReadAllText("items.def.txt");
var txt = Scr.FromTxt(txt, SerializeFormat.INI);
File.WriteAllLBytes("items.def", txt);
```
**Batch convert a directory of various (RTXT, CBIN, SCR) files to .txt :**
```csharp
using NHQTools.FileFormats;

foreach (var file in Directory.GetFiles("dir")) 
{
    var bytes = File.ReadAllBytes(file);
    var type = Definitions.DetectType(bytes);

    if (type != FileType.Unknown) 
    {
        var formatDef = Definitions.GetFormatDef(type);
        if (formatDef.TextSerializer != null)
        {
            var txt = formatDef.TextSerializer.ToTxt(bytes, formatDef.SerializeFormats.First());
            File.WriteAllText($"{file}.txt", txt);
        }
    }
}
```
**Unpack and Pack a BFC container:**
```csharp
using NHQTools.FileFormats;

// Unpack
var targetFile = new FileInfo("example.dds");
var unpackedBytes = Bfc.Unpack(targetFile);
File.WriteAllBytes("example_unpacked.dds", unpackedBytes);

// Pack
var sourceFile = new FileInfo("example_unpacked.dds");
var packedBytes = Bfc.Pack(sourceFile);
File.WriteAllBytes("example_packed.dds", packedBytes);
```

**Extract the contents (3DOs, textures) of a PAK archive::**
```csharp
using NHQTools.FileFormats;

var pakFile = new FileInfo("example.pak");
var extractedFiles = Pak.Unpack(pakFile);

foreach (var file in extractedFiles) 
{
    File.WriteAllBytes(Path.Combine("output", file.Key), file.Value);
}
```

---

## Building from Source
1. **Requirements**: Windows 10+ with .NET Framework 4.8. Visual Studio 2022 or newer is recommended for designer support.
2. **Compile**: Open `NHQTools.sln`, set your configuration to `Release|Any CPU`, and build the solution to generate `NHQTools.dll`.

---