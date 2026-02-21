using System;
using System.Runtime.InteropServices;

namespace NHQTools.Helpers
{

    ////////////////////////////////////////////////////////////////////////////////////
    #region Console Exit Reason Class
    public class ConsoleExitReason
    {
        
        public int Code { get; }
        public string Message { get; }

        public static readonly ConsoleExitReason Success = new ConsoleExitReason(0, "Operation completed successfully");

        public static readonly ConsoleExitReason Exception = new ConsoleExitReason(1, "An unexpected error occurred");
        public static readonly ConsoleExitReason InvalidArguments = new ConsoleExitReason(2, "Invalid arguments");
        public static readonly ConsoleExitReason UserCancelled = new ConsoleExitReason(3, "Operation cancelled");
        public static readonly ConsoleExitReason Timeout = new ConsoleExitReason(4, "Operation timed out");

        public static readonly ConsoleExitReason FileNotFound = new ConsoleExitReason(10, "File not found");
        public static readonly ConsoleExitReason FilePermission = new ConsoleExitReason(11, "File access denied");
        public static readonly ConsoleExitReason FileLocked = new ConsoleExitReason(12, "File is currently in use");

        public static readonly ConsoleExitReason NetworkError = new ConsoleExitReason(20, "Network connection failed");
        public static readonly ConsoleExitReason DatabaseError = new ConsoleExitReason(21, "Database connection failed");

        ////////////////////////////////////////////////////////////////////////////////////
        private ConsoleExitReason(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region Console Window Helper Class
    public class ConsoleWindowHelper
    {

        ////////////////////////////////////////////////////////////////////////////////////
        public static void Exit(ConsoleExitReason reason, string message = null)
        {
            if (string.IsNullOrEmpty(message) && reason.Code != ConsoleExitReason.Success.Code)
                message = reason.Message; // Default message for non-success if message is empty

            if (!string.IsNullOrEmpty(message))
                ConsoleMessageHelper.WriteLine(message, reason.Code == ConsoleExitReason.Success.Code ? ConsoleColor.Green : ConsoleColor.Red);

            ConsoleMessageHelper.NewLine();

            // Keep output if launched directly
            if (WasLaunchedFromExplorer())
            {
                Console.WriteLine(" Press any key to exit...");
                Console.ReadKey();
            }

            Environment.Exit(reason.Code);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static bool WasLaunchedFromExplorer()
        {
            var processList = new uint[2];

            // Number of processes attached to this console
            var processCount = GetConsoleProcessList(processList, (uint)processList.Length);

            // If the count is 1, a console window was created for this process
            // If the count is > 1, we are sharing the console with a command prompt
            return processCount == 1;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Native Methods
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);
        #endregion

    }
    #endregion

}