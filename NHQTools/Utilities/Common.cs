using System;
using System.IO;
using System.Drawing;
using Microsoft.Win32;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NHQTools.Helpers;

namespace NHQTools.Utilities
{
    public class Common
    {
        public const int NlCodepage = 1252;
        public const string NlEncodingName = "Windows-1252";

        public static string ProgramFilesX86 => Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? Environment.GetEnvironmentVariable("ProgramFiles");

        public static List<string> DefaultOpenFileDirList = new List<string>
        {
            Path.Combine(ProgramFilesX86, "Steam", "steamapps", "common"),
            Path.Combine(ProgramFilesX86, "NovaLogic"),
        };

        ////////////////////////////////////////////////////////////////////////////////////
        public static void LaunchWebBrowser(string address)
        {
            try
            {
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Unable to open the web browser. Please visit {address} manually.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly Random _randomStr = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var result = new char[length];

            // Lock it, Random() is not thread-safe. If two threads call this at once, the internal
            // state of _random can corrupt, leading to it returning all zeros or infinite loops.
            lock (_randomStr)
            {
                for (var i = 0; i < length; i++)
                    result[i] = chars[_randomStr.Next(chars.Length)];

            }

            return new string(result);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static int WindowsVersion()
        {
            try
            {
                using (var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    // Windows 10+
                    var majorObj = reg?.GetValue("CurrentMajorVersionNumber");
                    if (majorObj is int major)
                        return major;

                    //  Windows versions (7, 8, 8.1)
                    var productName = reg?.GetValue("ProductName")?.ToString(); 
                    if (productName != null)
                    {
                        var parts = productName.Split(' ');
                        if (parts.Length > 1 && int.TryParse(parts[1], out var ver))
                            return ver;
                    }
                }
            }
            catch { /* Ignore */  }

            return Environment.OSVersion.Version.Major;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool IsLightColor(Color c)
        {
            // Calculate the perceived brightness (Luminance)
            var brightness = (c.R * 0.299) + (c.G * 0.587) + (c.B * 0.114);

            // If brightness is > 128, it's a lighter color
            return brightness > 128;
        }

    }

}