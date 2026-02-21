using System;
using System.IO;
using System.Threading;
using System.Reflection;

namespace NHQTools.Helpers
{
    public class ConsoleMessageHelper
    {
        // Public
        public static bool WriteLog { get; private set; }
        public static bool PrefixWithSpace { get; set; } = true;

        public static string LogFile { get; private set; } = string.Empty;

        public const ConsoleColor DefaultColor = ConsoleColor.Gray;

        // Private
        private const int SeparatorShortChars = 81;
        private const int SeparatorLongChars = 115;
        private const int CenterWidth = 80;

        private static readonly object _logLock = new object();
        private static StreamWriter _logStream;

        ////////////////////////////////////////////////////////////////////////////////////
        #region Logging
        public static string GetLogFilePath(string logFile)
        {
            var exePath = Assembly.GetEntryAssembly()?.Location ?? AppDomain.CurrentDomain.BaseDirectory;
            var appDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var appLog = Path.ChangeExtension(Path.GetFileName(exePath), "log");

            // Default log file
            if (string.IsNullOrEmpty(logFile))
                return Path.Combine(appDir, appLog);

            var fullPath = logFile;

            // Make path absolute if relative
            // Change extension to .log unless a fully qualified path with extension is given
            if (!Path.IsPathRooted(logFile))
                fullPath = Path.ChangeExtension(Path.Combine(appDir, logFile), "log");

            // If logFile is just a filename
            var isDirectory = fullPath.EndsWith(Path.DirectorySeparatorChar.ToString())
                               || fullPath.EndsWith(Path.AltDirectorySeparatorChar.ToString())
                               || string.IsNullOrEmpty(Path.GetExtension(fullPath));

            // Combine supplied dir with appLog
            // Or return the full user supplied path
            return isDirectory 
                ? Path.Combine(fullPath, appLog)
                : fullPath;

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void EnableLogging(bool writeLog, bool overwrite = false, string logFile = "")
        {
            WriteLog = writeLog;

            if (!WriteLog)
                return;

            try
            {
                logFile = GetLogFilePath(logFile);

                if(overwrite && File.Exists(logFile))
                    File.Delete(logFile);

                LogFile = logFile;

                var fs = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.Read);

                _logStream = new StreamWriter(fs)
                {
                    AutoFlush = true
                };

                AppDomain.CurrentDomain.ProcessExit += (s, e) => CloseLog();

            }
            catch(Exception ex)
            {
                _logStream = null;
                WriteLog = false;

                // Error will not attempt to log again because WriteLog is false
                Error(ex);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static void AppendLog(string text)
        {
            if (!WriteLog || _logStream == null )
                return;

            try
            {
                lock (_logLock)
                {
                    _logStream.Write(text);
                }
            }
            catch
            {
                // Fail silently to preserve console UX
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static void CloseLog()
        {
            if (_logStream == null)
                return;

            _logStream.Flush();
            _logStream.Dispose();
            _logStream = null;
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Write
        public static void Write(string text = "", ConsoleColor color = DefaultColor, bool centerText = false)
        {
            text = centerText ? CenterText(text) : text;

            AppendLog(text);  // Don't log first space char

            Console.ForegroundColor = color;
            Console.Write(PrefixWithSpace ? " " + text : text);
            Console.ForegroundColor = DefaultColor;
        }

        // Write overloads
        public static void Write(char ch, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(ch.ToString(), color, centerText);
        public static void Write(Exception ex, ConsoleColor color = DefaultColor)
            => Error(ex, color);

        // Write Overloads for string.Format with up to 6 args
        public static void Write(string format, object args, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, args), color, centerText);
        public static void Write(string format, object arg0, object arg1, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, arg0, arg1), color, centerText);
        public static void Write(string format, object arg0, object arg1, object arg2, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, arg0, arg1, arg2), color, centerText);
        public static void Write(string format, object arg0, object arg1, object arg2, object arg3, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, arg0, arg1, arg2, arg3), color, centerText);
        public static void Write(string format, object arg0, object arg1, object arg2, object arg3, object arg4, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, arg0, arg1, arg2, arg3, arg4), color, centerText);
        public static void Write(string format, object arg0, object arg1, object arg2, object arg3, object arg4, object arg5, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, arg0, arg1, arg2, arg3, arg4, arg5), color, centerText);

        // Write Args objects[] overload
        public static void Write(string format, object[] args, ConsoleColor color = DefaultColor, bool centerText = false)
            => Write(string.Format(format, args), color, centerText);

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region WriteLine
        public static void WriteLine(string text = "", ConsoleColor color = DefaultColor, bool centerText = false)
        {
            text = centerText ? CenterText(text) : text;

            AppendLog(text + Environment.NewLine);  // Don't log first space char

            Console.ForegroundColor = color;
            Console.WriteLine(PrefixWithSpace ? " " + text : text);
            Console.ForegroundColor = DefaultColor;
        }

        // WriteLine overloads
        public static void WriteLine(char ch, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(ch.ToString(), color, centerText);
        public static void WriteLine(char[] ch, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(new string(ch), color, centerText);
        public static void WriteLine(Exception ex, ConsoleColor color = DefaultColor)
            => Error(ex, color);

        // WriteLine Overloads for string.Format with up to 6 args
        public static void WriteLine(string format, object args, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(string.Format(format, args), color, centerText);
        public static void WriteLine(string format, object arg0, object arg1, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(string.Format(format, arg0, arg1), color, centerText);
        public static void WriteLine(string format, object arg0, object arg1, object arg2, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(string.Format(format, arg0, arg1, arg2), color, centerText);
        public static void WriteLine(string format, object arg0, object arg1, object arg2, object arg3, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(string.Format(format, arg0, arg1, arg2, arg3), color, centerText);
        public static void WriteLine(string format, object arg0, object arg1, object arg2, object arg3, object arg4, ConsoleColor color = DefaultColor, bool centerText = false)
            =>  WriteLine(string.Format(format, arg0, arg1, arg2, arg3, arg4), color, centerText);
        public static void WriteLine(string format, object arg0, object arg1, object arg2, object arg3, object arg4, object arg5, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(string.Format(format, arg0, arg1, arg2, arg3, arg4, arg5), color, centerText);

        // WriteLine Args objects[] overload
        public static void WriteLine(string format, object[] args, ConsoleColor color = DefaultColor, bool centerText = false)
            => WriteLine(string.Format(format, args), color, centerText);

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Error messages

        // AppendLog called first to avoid logging first space char
        // AppendLog will not infinite loop because WriteLog is false during Error calls from EnableLogging failure
        public static void Error(string text = "", ConsoleColor color = ConsoleColor.Red)
        {
            AppendLog(text + Environment.NewLine);  // Don't log first space char

            Console.ForegroundColor = color;
            Console.WriteLine(PrefixWithSpace ? " " + text : text);
            Console.ForegroundColor = DefaultColor;
        }
        public static void Error(Exception ex, ConsoleColor color = ConsoleColor.Red)
            => Error(ex.Message, color);

        // Error Overloads for string.Format with up to 6 args
        public static void Error(string format, object arg1, ConsoleColor color = ConsoleColor.Red)
            => Error(string.Format(format, arg1), color);
        public static void Error(string format, object arg1, object arg2, ConsoleColor color = ConsoleColor.Red)
            => Error(string.Format(format, arg1, arg2), color);
        public static void Error(string format, object arg1, object arg2, object arg3, ConsoleColor color = ConsoleColor.Red)
            => Error(string.Format(format, arg1, arg2, arg3), color);
        public static void Error(string format, object arg1, object arg2, object arg3, object arg4, ConsoleColor color = ConsoleColor.Red)
            => Error(string.Format(format, arg1, arg2, arg3, arg4), color);
        public static void Error(string format, object arg1, object arg2, object arg3, object arg4, object arg5, ConsoleColor color = ConsoleColor.Red)
            => Error(string.Format(format, arg1, arg2, arg3, arg4, arg5), color);

        // Error Args objects[] overload
        public static void Error(string format, object[] args, ConsoleColor color = ConsoleColor.Red)
            => Error(string.Format(format, args), color);

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region New Line
        public static void NewLine()
        {
            AppendLog(Environment.NewLine);
            Console.WriteLine(" ");
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Separators
        public static void Separator(char str = '-', ConsoleColor color = DefaultColor)
            => WriteLine(new string(str, SeparatorShortChars), color);

        ////////////////////////////////////////////////////////////////////////////////////
        public static void SeparatorLong(char str = '-', ConsoleColor color = DefaultColor)
            => WriteLine(new string(str, SeparatorLongChars), color);
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        public static void ClearCurrentLine()
        {
            var top = Console.CursorTop;

            Console.SetCursorPosition(0, Console.CursorTop);

            for (var i = 0; i < Console.WindowWidth; i++)
                Console.Write(" ");

            Console.SetCursorPosition(0, top);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static string CenterText(string text) //70nhq
        {
            text = text?.Replace(Environment.NewLine, string.Empty);

            return string.IsNullOrEmpty(text) 
                ? string.Empty 
                : string.Format("{0," + ((CenterWidth / 2) + (text.Length / 2)) + "}", text);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool Confirm(string text = "")
        {

            Write(text + " [y/n]: ");

            var conf = Console.ReadKey().Key;

            Console.WriteLine();
            AppendLog(Environment.NewLine + conf + Environment.NewLine);


            return conf == ConsoleKey.Y;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void WaitingDots(int sleep = 250, bool newLine = false, ConsoleColor color = DefaultColor)
        {
            Thread.Sleep(sleep);

            Console.ForegroundColor = color;

            Console.Write(".");
            AppendLog(".");
            Thread.Sleep(sleep);

            Console.Write(".");
            AppendLog(".");
            Thread.Sleep(sleep);

            Console.Write(". ");
            AppendLog(".");
            Thread.Sleep(sleep);

            if (newLine)
            {
                Console.WriteLine();
                AppendLog(Environment.NewLine);
            }
               
            Console.ForegroundColor = DefaultColor;
        }

    }

}
