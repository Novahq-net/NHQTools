using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

// NHQTools Libraries
using NHQTools.Helpers;

// ReSharper disable VirtualMemberCallInConstructor

namespace NHQTools.Themes
{
    public class BaseFormTheme : Form
    {

        // Public
        public ResourceManager ThemeResources { get; set; }
        public string InitialTheme { get; set; } = null;
        public ThemeManager ThemeManager { get; private set; }
        public Font FontTextBox { get; }
        public Font FontTextBoxMultiLine { get; }
        public Font FontComboBox { get; }
        public Font FontDataGridCellHeader { get; }
        public string ImgButtonHoverPrefix { get; set; } = "PbHover";

        //////////////////////////////////////////////////////////////////////////////////////
        public BaseFormTheme()
        {
            // **** MAKE SURE TO DISPOSE OF THE FONTS IN THE OVERRIDE OF DISPOSE() ****
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Verdana", 7.5f, FontStyle.Regular);
            FontComboBox = new Font("Verdana", Font.Size * 1.0f, FontStyle.Regular);
            FontTextBox = new Font("Consolas", Font.Size * 1.1f, FontStyle.Regular);
            FontTextBoxMultiLine = new Font("Consolas", Font.Size * 1.2f, FontStyle.Regular);
            FontDataGridCellHeader = new Font("Verdana", Font.Size * 1.0f, FontStyle.Bold);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        protected override void OnLoad(EventArgs e)
        {
            ApplyBaseStyles(this);

            if (ThemeManager == null)
            {
                var theme = !string.IsNullOrEmpty(InitialTheme) ? 
                    InitialTheme 
                    : ThemeManager.CurrentTheme.ToString();

                ThemeManager = new ThemeManager(this, ThemeResources);
                ThemeManager.Apply(theme);
            }

            // Apply helpers after theme so controls have their final handles/images
            ApplyHelpers(this);

            // Load child form after applying theme to ensure they get the correct styles
            base.OnLoad(e);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void ApplyHelpers(Control parent)
        {
            // Image button hover effects for PictureBox controls matching the prefix
            if (ThemeResources != null && !string.IsNullOrEmpty(ImgButtonHoverPrefix))
                ImgButtonHoverHelper.Apply(parent, ImgButtonHoverPrefix, ThemeResources);

            // Placeholder text for TextBox controls with an AccessibleDescription set
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox tb && !string.IsNullOrWhiteSpace(tb.AccessibleDescription))
                    TextBoxPlaceholderHelper.Apply(tb);

                if (c.HasChildren)
                    ApplyHelpers(c);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////
        private void ApplyBaseStyles(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                switch (c)
                {
                    case StatusStrip _:
                    case MenuStrip _:
                    case ToolStrip _:
                        c.Font = Font;
                        break;
                    case TextBox tb when tb.Multiline:
                        tb.Font = FontTextBoxMultiLine;
                        break;
                    case TextBox tb:
                        tb.Font = FontTextBox;
                        break;
                    case ComboBox cb:
                        cb.Font = FontComboBox;
                        break;
                    case Button btn:
                        btn.BackColor = SystemColors.ControlLight;
                       
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(240, 240, 240);
                        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
                        btn.FlatAppearance.BorderColor = SystemColors.ControlDark;

                        break;
                    case DataGridView dgv:
                        dgv.EnableHeadersVisualStyles = false;
                        dgv.ColumnHeadersDefaultCellStyle.Font = FontDataGridCellHeader;
                        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
                        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 240, 240);
                        break;
                }

                if (c.HasChildren)
                    ApplyBaseStyles(c);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager?.Dispose();
                FontTextBox?.Dispose();
                FontTextBoxMultiLine?.Dispose();
                FontComboBox?.Dispose();
                FontDataGridCellHeader?.Dispose();
            }

            base.Dispose(disposing);
        }

    }

}