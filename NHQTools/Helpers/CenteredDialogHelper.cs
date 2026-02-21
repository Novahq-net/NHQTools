using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

// ReSharper disable IdentifierTypo
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace NHQTools.Helpers
{
    public class CenteredDialogHelper
    {
        ////////////////////////////////////////////////////////////////////////////////////
        public class CenterDialog : IDisposable
        {
            private readonly IWin32Window _owner;
            private int _hook;
            private readonly NativeMethods.CbtHookProc _proc;

            ////////////////////////////////////////////////////////////////////////////////////
            public CenterDialog(IWin32Window owner)
            {
                _owner = owner;
                _proc = HookProc;

                // Get the Thread ID of the current GUI thread
                var threadId = NativeMethods.GetCurrentThreadId();

                // Install a window listening Hook that listens for dialog creation
                _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_CBT, _proc, IntPtr.Zero, threadId);
            }

            ////////////////////////////////////////////////////////////////////////////////////
            private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
            {
                // HCBT_ACTIVATE = 5, Sent when a window is about to be activated.

                // HCBT_ACTIVATE = 5
                if (nCode == 5)
                {
                    // Verify owner is valid and on the same thread before touching UI properties
                    if (_owner is Control c && c.InvokeRequired)
                    {
                        // If we are here, we are in trouble. We can't touch the parent form bounds 
                        // from this thread. Fallback to default centering or screen center.
                    }
                    else
                    {
                        CenterWindow(wParam);
                    }

                    // Unhook immediately so we don't interfere with anything else
                    Dispose();
                }

                return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            ////////////////////////////////////////////////////////////////////////////////////
            private void CenterWindow(IntPtr hDialog)
            {

                // Get the bounds of the dialog
                NativeMethods.GetWindowRect(hDialog, out var dialogRect);
                var dialogWidth = dialogRect.Right - dialogRect.Left;
                var dialogHeight = dialogRect.Bottom - dialogRect.Top;

                // Get the bounds of the parent form
                // (We handle the case where parent might be a Control or Handle)
                Rectangle parentRect;
                switch (_owner)
                {
                    // Handle cases where owner is a Control vs just a Handle
                    case Control control:
                        parentRect = control.RectangleToScreen(control.ClientRectangle);
                        break;
                    case IWin32Window win:
                        // Fallback for non-Control IWin32Window implementations
                        parentRect = Screen.FromHandle(win.Handle).WorkingArea;
                        break;
                    default:
                        parentRect = Screen.FromHandle(hDialog).WorkingArea;
                        break;
                }

                // Calculate center from parentRect
                var x = parentRect.Left + (parentRect.Width - dialogWidth) / 2;
                var y = parentRect.Top + (parentRect.Height - dialogHeight) / 2;

                // Move the Window
                NativeMethods.SetWindowPos(hDialog, IntPtr.Zero, x, y, 0, 0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            }

            ////////////////////////////////////////////////////////////////////////////////////
            public void Dispose()
            {
                if (_hook == 0) 
                    return;

                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = 0;
            }

            ////////////////////////////////////////////////////////////////////////////////////
            #region Native Methods
            private static class NativeMethods
            {
                public const int WH_CBT = 5;
                public const uint SWP_NOSIZE = 0x0001;
                public const uint SWP_NOZORDER = 0x0004;
                public const uint SWP_NOACTIVATE = 0x0010;

                public delegate IntPtr CbtHookProc(int nCode, IntPtr wParam, IntPtr lParam);

                [DllImport("user32.dll")]
                public static extern int SetWindowsHookEx(int idHook, CbtHookProc lpfn, IntPtr hInstance, uint threadId);

                [DllImport("user32.dll")]
                public static extern bool UnhookWindowsHookEx(int idHook);

                [DllImport("user32.dll")]
                public static extern IntPtr CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);

                [DllImport("kernel32.dll")]
                public static extern uint GetCurrentThreadId();

                [DllImport("user32.dll")]
                public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

                [DllImport("user32.dll")]
                public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

                [StructLayout(LayoutKind.Sequential)]
                public struct RECT
                {
                    public int Left;
                    public int Top;
                    public int Right;
                    public int Bottom;
                }

            }
            #endregion

        }
    }
}