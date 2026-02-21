using System;
using System.IO;

namespace NHQTools.Exceptions
{
    [Serializable]
    public class FileNameException : IOException
    {
        public string FileName { get; }

        public FileNameException()
            : base("The file already exists.") {}

        public FileNameException(string message)
            : base(message) {}

        public FileNameException(string message, Exception innerException)
            : base(message, innerException) {}

        public FileNameException(string message, string fileName)
            : base(message) => FileName = fileName;
        
        public FileNameException(string fileName, bool generateMessage)
            : base($"The file '{fileName}' is not valid.") => FileName = fileName;
       
    }

}