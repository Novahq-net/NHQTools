using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace NHQTools.Helpers
{
    public static class FileNameHelper
    {

        private static readonly char[] InvalidFileSystemCharsArray = Path.GetInvalidFileNameChars();
        private static readonly HashSet<char> InvalidFileSystemChars = new HashSet<char>(InvalidFileSystemCharsArray);
        private static readonly string[] WindowsReservedNames = {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        private static string RandomFileName => Path.GetRandomFileName();
        private static string RandomFileNameWithoutExt => Path.GetFileNameWithoutExtension(RandomFileName);


        ////////////////////////////////////////////////////////////////////////////////////
        public static bool IsWindowsReservedName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var name = Path.GetFileName(fileName);

            name = Path.GetFileNameWithoutExtension(name).TrimEnd(' ', '.');

            if (name.Length == 0)
                return false;

            // Match exact device names
            if (name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("NUL", StringComparison.OrdinalIgnoreCase))
                return true;

            // COM1–COM9, LPT1–LPT9
            if (name.Length != 4) 
                return false;

            return (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                   char.IsDigit(name[3]) &&
                   name[3] != '0';
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static string SanitizeAsciiFileName(string name, char replacement = '_')
        {
            if (string.IsNullOrEmpty(name))
                return RandomFileNameWithoutExt;

            StringBuilder sb = null;

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                var bad = (c < 32) || (c > 126) || InvalidFileSystemChars.Contains(c);

                // Only allocate StringBuilder if we find a bad char
                // If we find a bad char we copy all the good chars up to that point
                // and then continue building the sanitized name with string builder
                if (!bad)
                {
                    sb?.Append(c);
                    continue;
                }

                if (sb == null)
                {
                    sb = new StringBuilder(name.Length);
                    sb.Append(name, 0, i); // copy the good prefix once
                }

                sb.Append(replacement);
            }

            var result = (sb == null ? name : sb.ToString()).Trim();
            return result.Length > 0 && !IsWindowsReservedName(result) ? result : RandomFileNameWithoutExt;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static string SanitizeAsciiFileName(string name, int maxLength, char replacement = '_')
        {
            if (string.IsNullOrEmpty(name))
                return "file_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName());

            var cleanName = SanitizeAsciiFileName(name, replacement);

            // Return early if within length, there is no need to truncate
            if (cleanName.Length <= maxLength)
                return cleanName;

            // Handle No Extension or DotFiles (.superlongweirdext, .superlongmoreweirdext)
            // If the file starts with a dot, we treat the whole thing as a name, 
            // because truncating ".superlongweirdext" to ".sup" changes the meaning entirely
            var extStart = cleanName.LastIndexOf('.');
            if (extStart <= 0)
            {
                var tmp1 = cleanName.Substring(0, maxLength);
                return IsWindowsReservedName(tmp1) ? RandomFileNameWithoutExt.PadRight(maxLength, '_').Substring(0, maxLength) : tmp1;
            }

            // Get the extension, hopefully it never comes to this
            var ext = cleanName.Substring(extStart);
            var extLength = ext.Length;

            // If the extension alone exceeds maxLength we truncate
            // and start pointing fingers at the user
            if (extLength >= maxLength)
            {
                var tmp2 = cleanName.Substring(0, maxLength);
                return IsWindowsReservedName(tmp2) ? RandomFileNameWithoutExt.PadRight(maxLength, '_').Substring(0, maxLength) : tmp2;
            }
            
            // Calculate remaining length for the name part
            var maxBaseNameLength = maxLength - extLength;

            // Truncate name while preserving extension
            // SomeSuperLongFileName.pcx > SomeSuperLo.pcx
            var newName = cleanName.Substring(0, maxBaseNameLength) + ext;

            return IsWindowsReservedName(newName) ? RandomFileNameWithoutExt.PadRight(maxLength, '_').Substring(0, maxLength) : newName;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Validation Status and Result

        // Validation status codes
        // Example Usage:
        // var opts = new ValidationOptions();
        //     opts.MinLength = new FileNameRule<int>(1, "Filename is too short.");
        //     opts.MaxLength = new FileNameRule<int>(15, "Filename is too long.");
        //     opts.InvalidStrings = new FileNameRule<string[]>(
        //     new string[] { "HEY", "YOU", "DOO" },
        //     "Filename contains a reserved word."
        // );
        public enum ValidationStatus
        {
            Ok,
            NullOrWhiteSpace,
            TooShort,
            TooLong,
            WindowsReservedName,
            ContainsInvalidString,
            ContainsInvalidChar,
            ContainsInvalidFileSystemChar,
            ContainsNonAscii
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public class FileNameRule<T>
        {
            public T Value { get; set; }
            public string AltErrorMessage { get; set; }

            public FileNameRule(T value, string errorMessage = null)
            {
                Value = value;
                AltErrorMessage = errorMessage;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public class ValidationOptions
        {
            public FileNameRule<char[]> InvalidChars { get; set; } = null; // Default invalid chars
            public FileNameRule<string[]> InvalidStrings { get; set; } = null;
            public FileNameRule<int> MinLength { get; set; } = new FileNameRule<int>(1); // Default min length is 1
            public FileNameRule<int> MaxLength { get; set; } = new FileNameRule<int>(255); // Default max length is 255 for ntfs
            public FileNameRule<bool> EnforceAscii { get; set; } = new FileNameRule<bool>(false); // Default is no ASCII enforcement
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public class ValidationResult
        {
            public bool IsValid { get; private set; }
            public ValidationStatus Status { get; private set; }
            public string Message { get; private set; }
            public object RejectedValue { get; private set; }
            public object Constraint { get; private set; }
            public string FileName { get; private set; }

            // Success
            public static ValidationResult Success(string fileName)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Status = ValidationStatus.Ok,
                    FileName = fileName,
                };
            }

            // Failed
            public static ValidationResult Failed(
                ValidationStatus status,
                string filename,
                string message,
                string altMessage,
                object rejectedValue,
                object constraint)
            {

                return new ValidationResult
                {
                    IsValid = false,
                    FileName = filename,
                    Status = status,
                    Message = string.IsNullOrEmpty(altMessage) ? message : altMessage,
                    RejectedValue = rejectedValue,
                    Constraint = constraint
                };

            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static ValidationResult ValidFileName(string fileName, ValidationOptions options = null)
        {
            options = options ?? new ValidationOptions();

            // Null or Whitespace
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return ValidationResult.Failed(
                    ValidationStatus.NullOrWhiteSpace,
                    fileName,
                    "Filename cannot be empty",
                    string.Empty,
                    fileName,
                    false
                );
            }

            // Trim whitespace before further checks
            fileName = fileName.Trim();

            if (fileName == "." || fileName == "..")
            {
                return ValidationResult.Failed(
                    ValidationStatus.ContainsInvalidString,
                    fileName,
                    $"Filename cannot be: '{fileName}'",
                    string.Empty,
                    fileName,
                    new[] { ".", ".." }
                );
            }

            // Invalid FileSystem Chars
            var fsCharIndex = fileName.IndexOfAny(InvalidFileSystemCharsArray);
            if (fsCharIndex >= 0)
            {
                var badChar = fileName[fsCharIndex];

                return ValidationResult.Failed(
                    ValidationStatus.ContainsInvalidFileSystemChar,
                    fileName,
                    $"Filename contains invalid character: '{badChar}'.",
                    string.Empty, // no custom message
                    badChar,
                    InvalidFileSystemCharsArray
                );
            }

            // Invalid User Specified Chars
            if (options.InvalidChars?.Value != null && options.InvalidChars.Value.Length > 0)
            {
                var charIndex = fileName.IndexOfAny(options.InvalidChars.Value);
                if (charIndex >= 0)
                {
                    var badChar = fileName[charIndex];
                    return ValidationResult.Failed(
                        ValidationStatus.ContainsInvalidChar,
                        fileName,
                        $"Filename character not allowed: '{badChar}'",
                        options.InvalidChars.AltErrorMessage,
                        badChar,
                        options.InvalidChars.Value
                    );
                }
            }

            // Windows Reserved Names
            if (IsWindowsReservedName(fileName))
            {
                return ValidationResult.Failed(
                    ValidationStatus.WindowsReservedName,
                    fileName,
                    $"'{fileName}' is a reserved system name",
                    string.Empty, // no custom message
                    fileName,
                    WindowsReservedNames
                );
            }

            // Min Length
            if (options.MinLength != null && fileName.Length < options.MinLength.Value)
            {
                return ValidationResult.Failed(
                    ValidationStatus.TooShort,
                    fileName,
                    $"Length {fileName.Length} is less than minimum {options.MinLength.Value} chars.",
                    options.MinLength.AltErrorMessage,
                    fileName.Length,
                    options.MinLength.Value
                );
            }

            // Max Length
            if (options.MaxLength != null && options.MaxLength.Value > 0 && fileName.Length > options.MaxLength.Value)
            {
                return ValidationResult.Failed(
                    ValidationStatus.TooLong,
                    fileName,
                    $"Length {fileName.Length} exceeds maximum of {options.MaxLength.Value} chars.",
                    options.MaxLength.AltErrorMessage,
                    fileName.Length,
                    options.MaxLength.Value
                );
            }

            // Strings
            var str = options.InvalidStrings?.Value?.FirstOrDefault(w => fileName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            if (str != null)
            {
                return ValidationResult.Failed(
                    ValidationStatus.ContainsInvalidString,
                    fileName,
                    $"Filename cannot contain: '{str}'.",
                    options.InvalidStrings.AltErrorMessage,
                    str,
                    options.InvalidStrings.Value
                );
            }

            // Printable ASCII
            // ReSharper disable once InvertIf
            if (options.EnforceAscii != null && options.EnforceAscii.Value)
            {
                foreach (var c in fileName)
                {
                    if (c < 32 || c > 126)
                    {
                        return ValidationResult.Failed(
                            ValidationStatus.ContainsNonAscii,
                            fileName,
                            $"Filename contains non-ASCII printable character: 0x{(int)c:X}.",
                            options.EnforceAscii.AltErrorMessage,
                            $"0x{(int)c:X}",
                            "0x20-0x7E"
                        );
                    }
                }
            }

            return ValidationResult.Success(fileName);
        }

        #endregion

    }

}