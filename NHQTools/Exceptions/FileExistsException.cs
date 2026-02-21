using System;
using System.IO;

namespace NHQTools.Exceptions
{
    [Serializable] 
    public class FileExistsException : IOException
    {
        public string FileName { get; }

        public FileExistsException()
            : base("The file already exists.") {}

        public FileExistsException(string message)
            : base(message) {}

        public FileExistsException(string message, Exception innerException)
            : base(message, innerException) {}

        public FileExistsException(string message, string fileName)
            : base(message) => FileName = fileName;

        public FileExistsException(string fileName, bool generateMessage)
            : base($"The file '{fileName}' already exists.") => FileName = fileName;
        
    }

}