using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Utilities;
using NHQTools.Extensions;

namespace NHQTools.FileFormats
{
    public enum CBinValueType
    {
        Int = 1,
        Float = 2,
        String = 4
    }

    /////////////////////////////////////////////////////////////////////////////////
    public class CBinFile
    {
        [Json.Exclude]
        public Encoding Enc { get; set; } = CBin.DefaultEnc;

        // Preserves the XOR key from the header so we can write it back
        [Json.Exclude]
        public uint EncryptionKey { get; set; } = CBin.DefaultEncryptionKey;

        // JSON-friendly hex representation of the encryption key
        public string EncryptionKeyHex
        {
            get => $"0x{EncryptionKey:X8}";
            set => EncryptionKey = Convert.ToUInt32(
                value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring(2)
                    : value, 16);
        }

        public List<CBinGroup> Groups { get; set; } = new List<CBinGroup>();
    }

    /////////////////////////////////////////////////////////////////////////////////
    public class CBinGroup
    {
        public string Section { get; set; }

        [Json.Exclude(Json.If.Empty)]
        public List<CBinKey> Entries { get; set; } = new List<CBinKey>();

        public CBinGroup() { }
        public CBinGroup(string section) => Section = section;
    }

    /////////////////////////////////////////////////////////////////////////////////
    public class CBinKey
    {
        public string Key { get; set; }

        // CBIN keys can have multiple values (e.g. Position = 100, 200, 0)
        public List<CBinValue> Values { get; set; } = new List<CBinValue>();

        [Json.Exclude]
        public string ValuesFormatted => string.Join(",", Values.Select(rv => rv.ValueFormatted))
            .Replace(",//,", "\t//")// KEYS can be commented individually, handle comments between values only
            .Replace(",//", "\t//"); // place comments at the end of the line and prevent edge cases where comments are empty

        public CBinKey() { }
        public CBinKey(string key) => Key = key;
    }

    /////////////////////////////////////////////////////////////////////////////////
    public class CBinValue
    {
        // 1=Int, 2=Float, 4=String
        public CBinValueType Type { get; set; }
        public string Value { get; set; }

        [Json.Exclude]
        public string ValueFormatted => Type == CBinValueType.String && Value != "//" ? $"\"{Value.EscapeQuotes()}\"" : Value;

        public CBinValue() { }
        public CBinValue(CBinValueType type, string value)
        {
            Type = type;
            Value = value;
        }

    }

}