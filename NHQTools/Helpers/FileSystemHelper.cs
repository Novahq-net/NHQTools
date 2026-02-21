using System;
using System.IO;

namespace NHQTools.Helpers
{
    public static class FileSystemHelper
    {

        ////////////////////////////////////////////////////////////////////////////////////
        #region File Read/Write/Create
        public static bool CanReadFile(string filePath)
        {
            // For existing files only

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                // Attempt to open with Read access
                using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return true;

            }
            catch {  return false; }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool CanWriteFile(string filePath)
        {
            // For existing files only

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                // Attempt to open the file with Write access
                // FileMode.Open because we only want to check existing files
                // FileShare.ReadWrite so we don't lock others out while checking
                using (File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    return true;
            }
            catch { return false; }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool CanCreateFile(string filePath, FileOptions options = FileOptions.DeleteOnClose)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var dir = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return false;

            try
            {
                var file = options == FileOptions.DeleteOnClose
                    ? Path.Combine(dir, Path.GetRandomFileName())
                    : filePath;

                using (File.Create(file, 1, options))
                    return true;
            }
            catch { return false; }
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Directory Read/Write/Create
        
        public static bool CanReadDirectory(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return false;

            try
            {
                _ = Directory.GetFileSystemEntries(dirPath);
                return true;
            }
            catch { return false; }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool CanWriteDirectory(string dirPath)
        {
            // For existing directories only

            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return false;

            try
            {
                var file = Path.Combine(dirPath, Path.GetRandomFileName());

                using (File.Create(file, 1, FileOptions.DeleteOnClose))
                    return true;
            }
            catch
            {
                return false;
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool CanCreateDirectory(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath))
                return false;

            var currentPath = dirPath;
            while (!Directory.Exists(currentPath))
            {
                var parent = Directory.GetParent(currentPath);

                if (parent == null || string.Equals(parent.FullName, currentPath, StringComparison.OrdinalIgnoreCase))
                    return false;

                currentPath = parent.FullName;
            }

            var temp = Path.Combine(currentPath, Path.GetRandomFileName());

            try
            {
                Directory.CreateDirectory(temp);
                Directory.Delete(temp);

                return true;
            }
            catch { return false; }

        }

#endregion

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool SetFileTimestamp(string filePath, DateTime timestampUtc, bool suppressErrors = false)
        {

            try
            {
                File.SetLastWriteTimeUtc(filePath, timestampUtc);
                File.SetCreationTimeUtc(filePath, timestampUtc);
                return true;
            }
            catch
            {

                if (!suppressErrors)
                    throw;

                return false;
                
            }

        }

    }

}