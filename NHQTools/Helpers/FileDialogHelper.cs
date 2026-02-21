using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace NHQTools.Helpers
{
    public class FileDialogHelper
    {

        ////////////////////////////////////////////////////////////////////////////////////
        public static string GetInitialDirectory(string lastDir, List<string> fallbackDirList)
        {

            if (!string.IsNullOrEmpty(lastDir) && Directory.Exists(lastDir))
                return lastDir;

            foreach (var dir in fallbackDirList.Where(Directory.Exists))
                return dir;

            return AppDomain.CurrentDomain.BaseDirectory;
        }

    }

}