using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace NHQTools.Helpers
{
    public static class TextBoxPlaceholderHelper
    {

        ////////////////////////////////////////////////////////////////////////////////////
        public static void Apply(TextBox textBox, string text) => SendMessage(textBox.Handle, EM_SETCUEBANNER, 0, text);

        ////////////////////////////////////////////////////////////////////////////////////
        public static void Apply(TextBox textBox)
        {
            if (!string.IsNullOrWhiteSpace(textBox.AccessibleDescription))
                SendMessage(textBox.Handle, EM_SETCUEBANNER, 0, textBox.AccessibleDescription);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Native methods
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);
        #endregion

    }
        
}
