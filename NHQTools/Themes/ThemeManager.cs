using System;
using System.Linq;
using System.Drawing;
using Microsoft.Win32;
using System.Resources;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NHQTools.Themes
{
    public class ThemeManager : IDisposable
    {
        public enum Themes
        {
            SystemDefault,
            Light,
            Dark
        }

        // Public
        public Form OwnerForm { get; }
        public ResourceManager Resources { get; }
        public static Themes CurrentTheme { get; private set; } = Themes.SystemDefault;

        // Colors
        public Color DarkModeTitleBar { get; set; } = Color.Black;
        public Color DarkModeGridHeader { get; set; } = Color.FromArgb(25, 25, 25);
        public Color DarkModeBackground { get; set; } = Color.FromArgb(32, 32, 32);
        public Color DarkModeSurface { get; set; } = Color.FromArgb(43, 43, 43);
        public Color DarkModeControl { get; set; } = Color.FromArgb(55, 55, 55);
        public Color DarkModeBorder { get; set; } = Color.FromArgb(100, 100, 100);
        public Color DarkModeText { get; set; } = Color.White;
        public Color DarkModeInactiveText { get; set; } = Color.FromArgb(176, 176, 176);
        public Color DarkModeItemSelection { get; set; } = ControlPaint.Light(Color.FromArgb(32, 32, 32), 0.15f);
        public Color DarkModeDataGridItemSelection { get; set; } = ControlPaint.Light(Color.FromArgb(32, 32, 32), 0.15f);

        // Private
        private readonly ControlEventHandler _mainFormHandler;
        private readonly ControlEventHandler _childControlHandler;
        private readonly Dictionary<Control, OriginalDesignerState> _originalColors = new Dictionary<Control, OriginalDesignerState>();

        // Contains the original colors and styles of controls as set in the designer
        private struct OriginalDesignerState
        {
            public Color BackColor;
            public Color BackgroundColor;
            public Color ForeColor;
            public BorderStyle BorderStyle;
            public FlatStyle FlatStyle;
            public Color FlatBorderColor;
            public Color FlatMouseDownColor;
            public Color FlatMouseOverColor;
            public Color FlatCheckedColor;
            public int FlatBorderSize;
            public Color GridColor;
            public DataGridViewCellStyle HeaderStyle;
            public DataGridViewCellStyle CellStyle;
            public bool EnableHeadersVisualStyles;
            public Image OriginalImage;
            public Image DarkImage;
            public Dictionary<int, Image> ColumnImages;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        public ThemeManager(Form form, ResourceManager resourceManager = null)
        {
            OwnerForm = form ?? throw new ArgumentNullException(nameof(form), "Form cannot be null.");
            Resources = resourceManager;

            _mainFormHandler = (s, e) => RegisterControlRecursive(e.Control);
            _childControlHandler = (s, e) => RegisterControlRecursive(e.Control);

            OwnerForm.ControlAdded += _mainFormHandler;
            OwnerForm.Disposed += (s, e) => Dispose();

            RegisterControlRecursive(OwnerForm);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        #region Theme Control / Register Control / Handlers
        private void ThemeControl(Control c, Themes theme)
        {
            var isDark = theme == Themes.Dark;

            if (!_originalColors.TryGetValue(c, out var original))
                return;

            ApplyNativeTheme(c, isDark);
            ApplyBaseColors(c, isDark, original);
            ApplyControlStyles(c, isDark, original);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void RegisterControlRecursive(Control c, bool recursive = true)
        {
            if (c is IDataGridViewEditingControl)
                return; // Do not track state or attach events (Causes crash)

            if (_originalColors.ContainsKey(c))
                return;

            var state = new OriginalDesignerState
            {
                BackColor = c.BackColor,
                ForeColor = c.ForeColor
            };

            switch (c)
            {
                case TextBoxBase tb:
                    state.BorderStyle = tb.BorderStyle;
                    break;
                case ButtonBase btn:
                    state.FlatStyle = btn.FlatStyle;
                    state.FlatBorderColor = btn.FlatAppearance.BorderColor;
                    state.FlatMouseDownColor = btn.FlatAppearance.MouseDownBackColor;
                    state.FlatMouseOverColor = btn.FlatAppearance.MouseOverBackColor;
                    state.FlatCheckedColor = btn.FlatAppearance.CheckedBackColor;
                    state.FlatBorderSize = btn.FlatAppearance.BorderSize;
                    break;
                case DataGridView dgv:
                    state.GridColor = dgv.GridColor;
                    state.BackgroundColor = dgv.BackgroundColor;
                    state.BorderStyle = dgv.BorderStyle;
                    state.HeaderStyle = dgv.ColumnHeadersDefaultCellStyle.Clone();
                    state.CellStyle = dgv.DefaultCellStyle.Clone();
                    state.EnableHeadersVisualStyles = dgv.EnableHeadersVisualStyles;
                    state.ColumnImages = new Dictionary<int, Image>();
                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        if (col is DataGridViewImageColumn imgCol && imgCol.Image != null)
                            state.ColumnImages[col.Index] = imgCol.Image;
                    }
                    break;
                case ComboBox cb:
                    state.FlatStyle = cb.FlatStyle;
                    break;
                case PictureBox pb:
                    state.OriginalImage = pb.Image;
                    if (Resources != null && pb.Tag is string resourceName)
                        state.DarkImage = Resources.GetObject(resourceName + "-dark") as Image ?? Resources.GetObject(resourceName + "_dark") as Image;
                    break;
            }

            _originalColors.Add(c, state);

            // Register events **AFTER** adding to dictionary to prevent re-entry
            c.ControlAdded += _childControlHandler;

            // Only subscribe to HandleCreated if the handle doesn't exist yet.
            if (!c.IsHandleCreated)
                c.HandleCreated += ControlHandleCreated;
            else if (CurrentTheme == Themes.Dark)
                ThemeControl(c, Themes.Dark); // Apply theme immediately if handle exists

            if (!recursive)
                return;

            foreach (Control child in c.Controls)
                RegisterControlRecursive(child, recursive: true);

        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void ControlHandleCreated(object sender, EventArgs e)
        {
            if (sender is Control c)
                ThemeControl(c, CurrentTheme);
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////
        #region Apply Theme
        public void Apply(string theme)
        {
            if (Enum.TryParse<Themes>(theme, true, out var parsedTheme))
            {
                Apply(parsedTheme);
                return;
            }
            Apply(Themes.SystemDefault);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        public void Apply(Themes theme)
        {
            CurrentTheme = theme;

            if (theme == Themes.SystemDefault)
                theme = GetSystemAppTheme();

            if (OwnerForm.IsHandleCreated)
                ApplyTitleBarTheme(theme);
            else
                OwnerForm.HandleCreated += (s, e) => ApplyTitleBarTheme(theme);

            ApplyTheme(OwnerForm, theme);
            OwnerForm.Invalidate(true);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void ApplyTheme(Control container, Themes theme)
        {
            ThemeControl(container, theme);
            foreach (Control c in container.Controls)
                ApplyTheme(c, theme);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void ApplyTitleBarTheme(Themes theme)
        {
            if (!OwnerForm.IsHandleCreated) return;

            var isDark = theme == Themes.Dark;
            var attributeValue = isDark ? 1 : 0;

            if (NativeMethods.DwmSetWindowAttribute(OwnerForm.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref attributeValue, sizeof(int)) != 0)
                NativeMethods.DwmSetWindowAttribute(OwnerForm.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref attributeValue, sizeof(int));

            NativeMethods.SendMessage(OwnerForm.Handle, NativeMethods.WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);
            NativeMethods.SendMessage(OwnerForm.Handle, NativeMethods.WM_NCACTIVATE, new IntPtr(1), IntPtr.Zero);
            NativeMethods.RedrawWindow(OwnerForm.Handle, IntPtr.Zero, IntPtr.Zero, NativeMethods.RDW_FRAME | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_UPDATENOW);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void ApplyNativeTheme(Control c, bool isDark)
        {
            // Sets the Win32 visual theme on the control's window handle.

            if (!c.IsHandleCreated || c is IDataGridViewEditingControl)
                return;

            // Buttons, labels, and toggle controls need an empty theme to render correctly in dark mode
            if (c is Button || c is GroupBox || c is CheckBox || c is RadioButton || c is Label)
            {
                NativeMethods.SetWindowTheme(c.Handle, "", null);
                return;
            }

            var themeName = isDark ? "DarkMode_Explorer" : "ClearMode_Explorer";
            NativeMethods.SetWindowTheme(c.Handle, themeName, null);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Applies foreground and background colors shared by all control types.
        private void ApplyBaseColors(Control c, bool isDark, OriginalDesignerState original)
        {
            if (!isDark)
            {
                c.ForeColor = original.ForeColor;
                c.BackColor = original.BackColor;
                return;
            }

            // Dark mode
            c.ForeColor = DarkModeText;

            switch (c)
            {
                // TextBox wrapper panels and their child textboxes share the same control color
                // so the textbox background blends with the panel used for vertical centering
                case Panel p when (p.Tag?.ToString() == "TextBoxWrapper" || (p.Controls.Count == 1 && p.Controls[0] is TextBoxBase)):
                case TextBoxBase _ when (c.Parent is Panel pp && pp.Tag?.ToString() == "TextBoxWrapper"):
                    c.BackColor = DarkModeControl;
                    break;
                case Form _:
                case Panel _:
                    c.BackColor = DarkModeBackground;
                    break;
                default:
                    {
                        if (c.BackColor != Color.Transparent)
                            c.BackColor = DarkModeSurface;
                        break;
                    }
            }
        
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Applies styling specific to individual control types (borders, flat styles, images, etc.)
        private void ApplyControlStyles(Control c, bool isDark, OriginalDesignerState original)
        {
            switch (c)
            {
                // Button
                case Button btn when isDark:
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.BorderColor = DarkModeBorder;
                    btn.BackColor = DarkModeControl;
                    btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(DarkModeControl, 0.10f);
                    btn.FlatAppearance.MouseDownBackColor = ControlPaint.Light(DarkModeControl, 0.25f);
                    break;
                case Button btn:
                    btn.FlatStyle = original.FlatStyle;
                    if (original.FlatStyle != FlatStyle.Standard && original.FlatStyle != FlatStyle.System)
                    {
                        btn.FlatAppearance.BorderColor = original.FlatBorderColor;
                        btn.FlatAppearance.MouseDownBackColor = original.FlatMouseDownColor;
                        btn.FlatAppearance.MouseOverBackColor = original.FlatMouseOverColor;
                        btn.FlatAppearance.CheckedBackColor = original.FlatCheckedColor;
                        btn.FlatAppearance.BorderSize = original.FlatBorderSize;
                    }
                    break;

                // TextBox / RichTextBox
                case TextBoxBase tb when isDark:
                    tb.BorderStyle = original.BorderStyle == BorderStyle.None ? BorderStyle.None : BorderStyle.FixedSingle;
                    break;
                case TextBoxBase tb:
                    tb.BorderStyle = original.BorderStyle;
                    break;

                // ListBox
                case ListBox lb when isDark:
                    lb.BorderStyle = original.BorderStyle == BorderStyle.None ? BorderStyle.None : BorderStyle.FixedSingle;
                    break;
                case ListBox lb:
                    lb.BorderStyle = original.BorderStyle;
                    break;

                // ComboBox
                case ComboBox cb when isDark:
                    if (cb.IsHandleCreated) NativeMethods.SetWindowTheme(cb.Handle, "DarkMode_CFD", null);
                    cb.FlatStyle = FlatStyle.Standard;
                    cb.BackColor = DarkModeSurface;
                    break;
                case ComboBox cb:
                    if (cb.IsHandleCreated) NativeMethods.SetWindowTheme(cb.Handle, "ClearMode_Explorer", null);
                    cb.FlatStyle = original.FlatStyle;
                    cb.BackColor = original.BackColor;
                    break;

                // PictureBox
                case PictureBox pb:
                    pb.Image = (isDark && original.DarkImage != null) ? original.DarkImage : original.OriginalImage;
                    break;

                // DataGridView
                case DataGridView dgv when isDark:
                    dgv.EnableHeadersVisualStyles = false;
                    dgv.BackgroundColor = DarkModeControl;
                    dgv.GridColor = DarkModeBorder;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = DarkModeGridHeader;
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = DarkModeText;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkModeSurface;
                    dgv.DefaultCellStyle.BackColor = DarkModeBackground;
                    dgv.DefaultCellStyle.ForeColor = DarkModeText;
                    dgv.DefaultCellStyle.SelectionBackColor = DarkModeDataGridItemSelection;
                    dgv.DefaultCellStyle.SelectionForeColor = DarkModeText;

                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        if (!(col is DataGridViewImageColumn imgCol) || !(col.Tag is string resourceName))
                            continue;

                        var darkImg = Resources?.GetObject(resourceName + "-dark") as Image
                                      ?? Resources?.GetObject(resourceName + "_dark") as Image;
                        if (darkImg != null) imgCol.Image = darkImg;
                    }
                    dgv.Paint -= DataGridViewsScrollJunctionPaint;
                    dgv.Paint += DataGridViewsScrollJunctionPaint;
                    break;

                case DataGridView dgv:
                    dgv.EnableHeadersVisualStyles = original.EnableHeadersVisualStyles;
                    dgv.BackgroundColor = original.BackgroundColor;
                    dgv.GridColor = original.GridColor;
                    if (original.HeaderStyle != null) dgv.ColumnHeadersDefaultCellStyle = original.HeaderStyle.Clone();
                    if (original.CellStyle != null) dgv.DefaultCellStyle = original.CellStyle.Clone();
                    if (original.ColumnImages != null)
                    {
                        foreach (var kvp in original.ColumnImages)
                        {
                            if (dgv.Columns.Count > kvp.Key && dgv.Columns[kvp.Key] is DataGridViewImageColumn imgCol)
                                imgCol.Image = kvp.Value;
                        }
                    }
                    dgv.Paint -= DataGridViewsScrollJunctionPaint;
                    break;

                // Transparent-aware controls
                case Label _:
                case CheckBox _:
                case RadioButton _:
                    if (isDark)
                        c.BackColor = c.BackColor == Color.Transparent ? Color.Transparent : c.Parent?.BackColor ?? DarkModeBackground;
                    break;

                // GroupBox — owner-draw border in dark mode to avoid bright white
                case GroupBox gb when isDark:
                    gb.BackColor = gb.BackColor == Color.Transparent ? Color.Transparent : gb.Parent?.BackColor ?? DarkModeBackground;
                    gb.Paint -= GroupBoxDarkPaint;
                    gb.Paint += GroupBoxDarkPaint;
                    break;
                case GroupBox gb:
                    gb.BackColor = gb.BackColor == Color.Transparent ? Color.Transparent : original.BackColor;
                    gb.Paint -= GroupBoxDarkPaint;
                    break;
            }

        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////
        private static Themes GetSystemAppTheme()
        {
            try
            {
                const string key = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                var val = Registry.GetValue(key, "AppsUseLightTheme", -1);
                if (val is int i && i >= 0)
                    return i == 0 ? Themes.Dark : Themes.Light;
            }
            catch { /* Ignore */ }
            return Themes.SystemDefault;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            if (OwnerForm != null) OwnerForm.ControlAdded -= _mainFormHandler;
            foreach (var control in _originalColors.Keys.Where(control => control != null && !control.IsDisposed))
            {
                control.ControlAdded -= _childControlHandler;
                control.HandleCreated -= ControlHandleCreated;
            }
            _originalColors.Clear();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        #region Dark Mode Painting

        // Colors the junction area between scrollbars in DataGridViews
        private void DataGridViewsScrollJunctionPaint(object sender, PaintEventArgs e)
        {
            if (!(sender is DataGridView dgv))
                return;

            var vScroll = dgv.Controls.OfType<VScrollBar>().FirstOrDefault(s => s.Visible);
            var hScroll = dgv.Controls.OfType<HScrollBar>().FirstOrDefault(s => s.Visible);
            var w = vScroll?.Width ?? 0;
            var h = hScroll?.Height ?? 0;

            if (w <= 0 || h <= 0)
                return;

            using (var brush = new SolidBrush(DarkModeControl))
                e.Graphics.FillRectangle(brush, dgv.ClientRectangle.Width - w, dgv.ClientRectangle.Height - h, w, h);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Colors the group box borders in dark mode
        private void GroupBoxDarkPaint(object sender, PaintEventArgs e)
        {
            if (!(sender is GroupBox gb))
                return;

            var textSize = TextRenderer.MeasureText(gb.Text, gb.Font);
            var borderRect = new Rectangle(
                0,
                textSize.Height / 2,
                gb.Width - 1,
                gb.Height - textSize.Height / 2 - 1);

            // Clear the background so the default white border is hidden
            var bgColor = gb.BackColor == Color.Transparent
                ? gb.Parent?.BackColor ?? DarkModeBackground
                : gb.BackColor;
            e.Graphics.Clear(bgColor);

            // Draw border with DarkModeBorder color
            using (var pen = new Pen(DarkModeBorder))
                e.Graphics.DrawRectangle(pen, borderRect);

            // Draw the title text, leaving a gap in the top border
            var textRect = new Rectangle(6, 0, textSize.Width + 2, textSize.Height);
            using (var bgBrush = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(bgBrush, textRect);

            TextRenderer.DrawText(e.Graphics, gb.Text, gb.Font, new Point(8, 0), DarkModeText);
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////
        #region Native Methods
        private static class NativeMethods
        {
            public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Windows 10 20H1+ (Build 19041) and Windows 11
            public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19; // Windows 10 1809 - 1909

            public const uint RDW_INVALIDATE = 0x0001;
            public const uint RDW_UPDATENOW = 0x0100;
            public const uint RDW_FRAME = 0x0400;

            public const int WM_NCACTIVATE = 0x0086;
            [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
            [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);
            [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
        }
        #endregion

    }

}