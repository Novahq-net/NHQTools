using System.Text;
using System.Collections.Generic;

// NHQTools Libraries
using NHQTools.Utilities;

namespace NHQTools.FileFormats
{
    /////////////////////////////////////////////////////////////////////////////////
    public class RTxtFile
    {
        [Json.Exclude]
        public Encoding Enc { get; set; } = RTxt.DefaultEnc;

        public List<RtxtGroup> Groups { get; set; } = new List<RtxtGroup>();
    }

    /////////////////////////////////////////////////////////////////////////////////
    public class RtxtGroup
    {
        [Json.Exclude]
        public int Id { get; set; }

        [Json.Exclude(Json.If.Null)]
        public string Section { get; set; } // Name of the group

        [Json.Exclude(Json.If.Empty)]
        public List<RtxtEntry> Entries { get; set; }

        public RtxtGroup() => Entries = new List<RtxtEntry>();
   
        public RtxtGroup(int id, string section) : this()
        {
            Id = id;
            Section = section;
        }
    }

    /////////////////////////////////////////////////////////////////////////////////
    public class RtxtEntry
    {
        [Json.Exclude(Json.If.Null)]
        public string Key { get; set; }

        public string Value { get; set; }

        [Json.Exclude(Json.If.Zero)]
        public short OffsetX { get; set; }

        [Json.Exclude(Json.If.Zero)]
        public short OffsetY { get; set; }

        public RtxtEntry() { }

        public RtxtEntry(string key, string value, short offsetX, short offsetY)
        {
            Key = key;
            Value = value;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

    }

}