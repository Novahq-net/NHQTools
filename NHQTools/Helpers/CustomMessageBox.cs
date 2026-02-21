using System;
using System.Drawing;
using System.Windows.Forms;

// NHQTools Libraries
using NHQTools.Themes;

namespace NHQTools.Helpers
{
    public sealed class CustomMessageBox : BaseFormTheme
    {
        // Public
        public static int FormWidth = 425;
        public static int ButtonWidth = 75;
        public static int ButtonHeight = 25;
        public static int PaddingStandard = 20;
        public static int IconSize = 32;
        public static int ButtonGap = 5;

        // Private
        private Label _lblMessage;
        private CheckBox _chkRemember;
        private PictureBox _picIcon;
        private Panel _pnlBottom;

        private Button _btn1;
        private Button _btn2;
        private Button _btn3;

        private readonly MessageBoxButtons _buttons;

        ////////////////////////////////////////////////////////////////////////////////////
        private CustomMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, string checkBoxText = null, bool initialCheckState = false, bool autoWidth = false)
        {
            Text = caption;
            Font = SystemFonts.MessageBoxFont; // Use system message box font
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            
            _buttons = buttons;

            InitializeControls(text, buttons, icon, checkBoxText, initialCheckState, autoWidth);

            ResizeForm(autoWidth);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Show Box
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            if (owner is Control control && control.InvokeRequired)
                return (DialogResult)control.Invoke(new Func<DialogResult>(() => Show(owner, text, caption, buttons, icon)));

            using (var form = new CustomMessageBox(text, caption, buttons, icon))
                return form.ShowDialog(owner);
        
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static DialogResult ShowCheckBox(IWin32Window owner, string text, string caption, string checkBoxText, Action<DialogResult, bool> checkBoxResultAction, bool defaultCheckState = false, MessageBoxButtons buttons = MessageBoxButtons.YesNo, MessageBoxIcon icon = MessageBoxIcon.Question)
        {
            if (owner is Control control && control.InvokeRequired)
                return (DialogResult)control.Invoke(new Func<DialogResult>(() => ShowCheckBox(owner, text, caption, checkBoxText, checkBoxResultAction, defaultCheckState, buttons, icon)));

            using (var form = new CustomMessageBox(text, caption, buttons, icon, checkBoxText, defaultCheckState))
            {
                var result = form.ShowDialog(owner);

                // Execute callback if a valid button was clicked (Yes/No/OK/Retry/etc)
                if (checkBoxResultAction != null && result != DialogResult.Cancel && result != DialogResult.Abort)
                    checkBoxResultAction(result, form._chkRemember.Checked);

                return result;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static DialogResult ShowAutoWidth(IWin32Window owner, string text, string caption, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            if (owner is Control control && control.InvokeRequired)
                return (DialogResult)control.Invoke(new Func<DialogResult>(() => ShowAutoWidth(owner, text, caption, buttons, icon)));

            using (var form = new CustomMessageBox(text, caption, buttons, icon, autoWidth: true))
                return form.ShowDialog(owner);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Controls
        private void InitializeControls(string text, MessageBoxButtons buttons, MessageBoxIcon icon, string checkBoxText, bool defaultCheckState, bool autoWidth)
        {
            // Icon
            var sysIcon = GetSystemIcon(icon);
            var hasIcon = (sysIcon != null);
            var textX = hasIcon ? (PaddingStandard + IconSize + PaddingStandard) : PaddingStandard;

            _picIcon = new PictureBox
            {
                Location = new Point(PaddingStandard, PaddingStandard),
                Size = new Size(IconSize, IconSize),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                Image = sysIcon?.ToBitmap(),
                Visible = hasIcon
            };
            Controls.Add(_picIcon);

            // Message Text
            _lblMessage = new Label
            {
                Text = text,
                Location = new Point(textX, PaddingStandard),
                AutoSize = true,
            };

            // Fix for auto expanding width
            _lblMessage.MaximumSize = autoWidth ? Size.Empty :
                new Size(FormWidth - textX - PaddingStandard, 0);

            Controls.Add(_lblMessage);

            // Bottom Panel
            _pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = SystemColors.Control
            };
            Controls.Add(_pnlBottom);

            // Optional Checkbox
            if (!string.IsNullOrEmpty(checkBoxText))
            {
                _chkRemember = new CheckBox
                {
                    Text = checkBoxText,
                    Checked = defaultCheckState,
                    AutoSize = true,
                    Location = new Point(PaddingStandard, 15)
                };
                _pnlBottom.Controls.Add(_chkRemember);
            }

            // Buttons
            AddButtons(buttons);
        }

        private void AddButtons(MessageBoxButtons buttons)
        {
            var x = _pnlBottom.Width - PaddingStandard;
            const int y = 12;

            // Helper to add a button and move X pointer left
            Button AddBtn(string text, DialogResult res)
            {
                var btn = CreateButton(text, res);
                btn.Location = new Point(x - btn.Width, y);
                _pnlBottom.Controls.Add(btn);
                x -= (btn.Width + ButtonGap);
                return btn;
            }

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    _btn1 = AddBtn("OK", DialogResult.OK);
                    AcceptButton = _btn1;
                    CancelButton = _btn1;
                    break;

                case MessageBoxButtons.OKCancel:
                    _btn2 = AddBtn("Cancel", DialogResult.Cancel);
                    _btn1 = AddBtn("OK", DialogResult.OK);
                    AcceptButton = _btn1;
                    CancelButton = _btn2;
                    break;

                case MessageBoxButtons.YesNo:
                    _btn2 = AddBtn("No", DialogResult.No);
                    _btn1 = AddBtn("Yes", DialogResult.Yes);
                    AcceptButton = _btn1;
                    CancelButton = _btn2;
                    break;

                case MessageBoxButtons.YesNoCancel:
                    _btn3 = AddBtn("Cancel", DialogResult.Cancel);
                    _btn2 = AddBtn("No", DialogResult.No);
                    _btn1 = AddBtn("Yes", DialogResult.Yes);
                    AcceptButton = _btn1;
                    CancelButton = _btn3;
                    break;

                case MessageBoxButtons.RetryCancel:
                    _btn2 = AddBtn("Cancel", DialogResult.Cancel);
                    _btn1 = AddBtn("Retry", DialogResult.Retry);
                    AcceptButton = _btn1;
                    CancelButton = _btn2;
                    break;

                case MessageBoxButtons.AbortRetryIgnore:
                    _btn3 = AddBtn("Ignore", DialogResult.Ignore);
                    _btn2 = AddBtn("Retry", DialogResult.Retry);
                    _btn1 = AddBtn("Abort", DialogResult.Abort);
                    AcceptButton = _btn3;
                    CancelButton = _btn1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttons), buttons, null);

            }

        }
        
        private static Button CreateButton(string text, DialogResult result)
        {
            return new Button
            {
                Text = text,
                DialogResult = result,
                Size = new Size(ButtonWidth, ButtonHeight),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Resize / Form Closing / System Icons

        private void ResizeForm(bool autoWidth)
        {
            // AutoWidth = Right Edge of label + Padding
            var requiredWidth = autoWidth
                ? _lblMessage.Right + PaddingStandard
                : FormWidth;

            // Don't shrink smaller than our min
            if (requiredWidth < FormWidth)
                requiredWidth = FormWidth;

            // Bottom of content (icon or label) + padding + bottom panel height
            var contentBottom = Math.Max(_lblMessage.Bottom, (_picIcon?.Bottom ?? 0));
            var requiredHeight = contentBottom + PaddingStandard + _pnlBottom.Height;

            ClientSize = new Size(requiredWidth, requiredHeight);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static Icon GetSystemIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Error: return SystemIcons.Error;
                case MessageBoxIcon.Question: return SystemIcons.Question;
                case MessageBoxIcon.Warning: return SystemIcons.Warning;
                case MessageBoxIcon.Information: return SystemIcons.Information;
                case MessageBoxIcon.None:
                default: return null;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason != CloseReason.UserClosing) 
                return;

            if (DialogResult != DialogResult.None) 
                return;

            // Handle X button behavior based on button set
            switch (_buttons)
            {
                case MessageBoxButtons.YesNo:
                    DialogResult = DialogResult.No;
                    break;
                case MessageBoxButtons.AbortRetryIgnore:
                    DialogResult = DialogResult.Abort;
                    break;
                case MessageBoxButtons.OK:
                    DialogResult = DialogResult.OK;
                    break;
                case MessageBoxButtons.OKCancel:
                case MessageBoxButtons.YesNoCancel:
                case MessageBoxButtons.RetryCancel:
                default:
                    DialogResult = DialogResult.Cancel;
                    break;
            }

        }
        #endregion

    }

}