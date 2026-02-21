## NovaHQ Tools for NovaLogic Games

Visit [https://novahq.net](https://novahq.net) for more information on NovaLogic file formats and tools to work with these files. 

This repository contains tools for reverse engineering, modifying, and managing files from old NovaLogic games. It is split into two main projects, with possibly more planned in the future. **SEE:** [Future Plans](#future-plans)

* **`NHQTools`**: A .NET Framework 4.8 class library handling the actual parsing, serialization, and format detection of various NovaLogic file formats.
* **`NovaPFF`**: A graphical PFF archive manager built on top of `NHQTools`.

### NovaPFF
A GUI tool for reading, writing, and creating NovaLogic PFF archives. It supports all variants (PFF0-PFF4) and handles their specific quirks like alignment and CRC calculations. 

Instead of just importing and exporting files, NovaPFF lets you interact with the contents directly:
* Preview textures
* Sample audio files
* Edit text-based formats directly inside the archive
* Optimize archives by cleaning up dead-space

![NovaPFF](/NovaPFF/Resources/NovaPFF.png)

### Supported Formats
The library includes a format registry (`Definitions` / `FormatDef`) and specialized serializers for:
* **Text/Data**: `.BIN`, `.DEF`, `.MNU`, and others via `Scr`, `RTxt`, `CBin`, and `Txt` serializers.
* **Images**: `.PCX`, `.DDS`, `.R16`, `.TGA`, `.FNT`
* **Containers**: `.PFF`, `.PAK`, `.BFC`
* **Audio**: `.WAV`

---

## Repository Layout
* `NovaPFF/` – The WinForms GUI application.
* `NHQTools/` – The core class library with all parsing and serialization logic.
* `NHQTools/FileFormats/` – Core file format definitions and serializers for various NovaLogic formats.
* `NHQTools/*/` – Shared library code, utilities, and common logic for both the library and GUI apps.

---

## Building from Source
1. **Requirements**: Windows 10+ with .NET Framework 4.8. Visual Studio 2022 or newer is recommended for designer support.
2. **Compile**: Open `NHQTools.sln`, set your configuration to `Release|Any CPU`, and build the solution to generate `NHQTools.dll` and `NovaPFF.exe`.

---

## Usage examples

**WARNING:** If your game is installed in the **Program Files** folder, you will need to run NovaPFF **as administrator** to have write access to the game files.  

To **avoid this requirement**, I recommend installing all NovaLogic games **outside of the Program Files** folder. Someplace like `C:\Users\<YourUsername>\Games` is a better option as you will have full read/write access without needing to run as administrator.  

**This is the #1 cause of issues for most users trying to modify their game files.**  

There is **no need** to run this app as administrator if the files you want to modify are located in a directory with write permissions for your user account. Your user account,
even if it is an administrator account, does not have write permissions to files in Program Files by default. 
This is a security feature of Windows to prevent unauthorized modifications to important system files and applications. **This is not an issue with the app itself.**

If you modify a PFF or other game file in the Program Files directory without admin permissions, the app will most likey save files to the **Virtual Store** 
instead of the actual game directory, which can cause confusion when trying to find your modified files. The modified files will also not properly load into the game if one of the original files is still present in the game directory, as the game will read the original file instead of the modified one in the Virtual Store.  

**The Virtual Store for Windows Vista+ is located at** `C:\Users\<YourUsername>\AppData\Local\VirtualStore` or `%LocalAppData%\VirtualStore`. Visit [Virtual Store explanation from Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/2639269/please-explain-virtualstore-for-non-experts)

### Using `NovaPFF` (please read above...)
1. Make backups
2. Open `NovaPFF.exe`.
3. Click stuff.

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
## Future Plans
A good chunk of older tools no longer function on modern systems, my goal is to build modern, up to date replacements that can run on Windows 10+ without compatibility issues.

As time and interest allow, I plan to add more formats and tools to this repository. My next focus will probably be a BMS to MIS converter for all games.

I also really want to add support for the older 3D formats like .3DO .3DI, but they are a bit more complex and I have no experience working with 3D models.

Contributions, format documentation, and issue reports from the community are always welcome.