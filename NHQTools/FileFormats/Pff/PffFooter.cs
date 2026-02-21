using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace NHQTools.FileFormats.Pff
{
    ////////////////////////////////////////////////////////////////////////////////////
    // The structure of fields in the footer depend on the specific PFF version. Some or all
    // fields may be absent in certain versions, even varying with the same version. For example, PFF0 files do not contain
    // any footer. PFF2 files end with a CRC value after the entry table, which may be considered a footer.
    // PFF3 files vary in footer content. Some files have no footer, some have only an IP address, while others
    // have all three fields. PFF4 files typically include all three fields.   
    // The IP address field may have been used to track the origin of the file, the exact use is not known.
    // The zero padding field is generally unused except in one PFF3 example (PFF3-Raptor-Common.pff) where it has bytes CC F2 AC DF (IPAddress)
    // The signature usually contains "KING". This signature is present in a lot of different areas on NovaLogic games. Probably the creator of the tools.
    internal class PffFooter
    {
        private static readonly byte[] EmptyByte = Array.Empty<byte>();

        public uint Length { get; private set; }
        public uint Offset { get; private set; } // Offset from start of file, directly after the Entry Table
        public byte[] IpAddress { get; private set; } // LAN_IP of the computer that created the PFF (Not always present)
        public byte[] ZeroPadding { get; private set; } // Always zero except for some early PFF2?  (Not always present)
        public byte[] Signature { get; private set; } // KING = (0x474E494B) (Not always present)
        public byte[] SignatureExtended { get; private set; }

        // Raw bytes 
        public byte[] RawBytes
        {
            get
            {
                if (Length == 0)
                    return EmptyByte;

                var bytes = new List<byte>();
                bytes.AddRange(IpAddress ?? EmptyByte);
                bytes.AddRange(ZeroPadding ?? EmptyByte);
                bytes.AddRange(Signature ?? EmptyByte);
                bytes.AddRange(SignatureExtended ?? EmptyByte);
                return bytes.ToArray();
            }

        }

        // Helpers, encoding is passed in from PffFile when needed
        public string GetSignatureStr(Encoding enc) => enc.GetString(Signature ?? EmptyByte).Replace("\0", "\\0");
        public string GetSignatureExtendedStr(Encoding enc) => enc.GetString(SignatureExtended ?? EmptyByte).Replace("\0", "\\0");

        // Footer signature for create / write =)
        private const string SignatureNovaHq = "NVHQ";

        ////////////////////////////////////////////////////////////////////////////////////
        internal PffFooter()
        {
            Length = 0;
        }

        internal PffFooter(PffVersion version)
        {
            Length = 0;

            if (version.HasFooterIpAddress)
            {
                IpAddress = new byte[4];
                Length += 4;
            }

            if (version.HasFooterZeroPadding)
            {
                ZeroPadding = new byte[4];
                Length += 4;
            }

            if (version.HasFooterSignature)
            {
                Signature = new byte[4];
                Length += 4;
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal static PffFooter Read(BinaryReader reader, PffVersion version)
        {
            var footer = new PffFooter()
            {
                Length = (uint)(reader.BaseStream.Length - reader.BaseStream.Position),
                Offset = (uint)reader.BaseStream.Position
            };

            if (version.HasFooterIpAddress)
                footer.IpAddress = reader.ReadBytes(4);

            if (version.HasFooterZeroPadding)
                footer.ZeroPadding = reader.ReadBytes(4);

            if (version.HasFooterSignature)
                footer.Signature = reader.ReadBytes(4);

            if (reader.BaseStream.Position < reader.BaseStream.Length)
                footer.SignatureExtended = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

            return footer;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        internal static void Write(BinaryWriter writer, PffFooter footer, PffVersion version, Encoding enc)
        {

            if (footer.IpAddress != null)
                writer.Write(footer.IpAddress);

            if (footer.ZeroPadding != null)
                writer.Write(footer.ZeroPadding);

            if (version.HasFooterSignature)
                writer.Write(enc.GetBytes(SignatureNovaHq));

        }

    }
}