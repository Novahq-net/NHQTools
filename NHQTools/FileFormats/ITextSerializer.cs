using System.Text;

namespace NHQTools.FileFormats
{
    ////////////////////////////////////////////////////////////////////////////////////
    #region Text Converter Interface
    public interface ITextSerializer
    {
        string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc);
        byte[] FromTxt(string textData, SerializeFormat format, Encoding enc);
    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region CBin
    public class CBinSerializer : ITextSerializer
    {
        public string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc) => CBin.ToTxt(fileData, format, enc);
        public byte[] FromTxt(string textData, SerializeFormat format, Encoding enc) => CBin.FromTxt(textData, format, enc);
    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region RTxt
    public class RTxtSerializer : ITextSerializer
    {
        public string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc) => RTxt.ToTxt(fileData, format, enc);
        public byte[] FromTxt(string textData, SerializeFormat format, Encoding enc) => RTxt.FromTxt(textData, format, enc);
    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region Txt
    public class TxtSerializer : ITextSerializer
    {
        public string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc) => Txt.ToTxt(fileData, format, enc);
        public byte[] FromTxt(string textData, SerializeFormat format, Encoding enc) => Txt.FromTxt(textData, format, enc);
    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region Scr
    public class ScrSerializer : ITextSerializer
    {

        public string ToTxt(byte[] fileData, SerializeFormat format, Encoding enc)
        {
            uint? encryptionKey = null;

            // Mad about it
            // If we're at this method and the file data does not start with "SCR1", it is because we overrode the type in DetectType.
            if (fileData.Length >= 3 &&
                (fileData[0] != (byte)'S'
                && fileData[1] != (byte)'C'
                && fileData[2] != (byte)'R'))
            {
                encryptionKey = Scr.DF1Key;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[ScrSerializer] encryptionKey: {(encryptionKey.HasValue ? $"0x{encryptionKey.Value:X8}" : "null")}");

            return Scr.ToTxt(fileData, format, enc, encryptionKey);
        }

        public byte[] FromTxt(string textData, SerializeFormat format, Encoding enc) => Scr.FromTxt(textData, format, enc); // do not pass encryption key here, as it is expected to be included in the text data if needed

    }
    #endregion

}
