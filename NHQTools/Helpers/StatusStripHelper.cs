using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace NHQTools.Helpers
{
    ////////////////////////////////////////////////////////////////////////////////////
    #region Bound Status Label Class
    public class BoundStatusLabel
    {

        // Adds prefix automatically when setting value
        public string Value 
        {
            get => _value;
            set
            {
                _value = value;
                _label.Text = $"{_prefix}{value}";
                //_label.Visible = !string.IsNullOrEmpty(value); // Auto-hide if empty
            }
        } 
        private string _value;

        // Toggle visibility
        public bool Visible
        {
            get => _label.Visible;
            set => _label.Visible = value;
        }

        // Toggle enabled
        public bool Enabled
        {
            get => _label.Enabled;
            set => _label.Enabled = value;
        }

        public Color ForeColor
        {
            get => _label.ForeColor;
            set => _label.ForeColor = value;
        }

        private readonly ToolStripStatusLabel _label;
        private readonly string _prefix;
        private readonly string _defaultValue;

        ////////////////////////////////////////////////////////////////////////////////////
        public BoundStatusLabel(ToolStripStatusLabel label, string prefix = "", string defaultValue = "")
        {
            _label = label;
            _prefix = prefix;
            _defaultValue = defaultValue;

            // Set initial state
            Value = _defaultValue;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public void Reset() => Value = _defaultValue;

        ////////////////////////////////////////////////////////////////////////////////////
        public void Hide() => Visible = false;

        ////////////////////////////////////////////////////////////////////////////////////
        public void Show() => Visible = true;

    }
    #endregion

    ////////////////////////////////////////////////////////////////////////////////////
    #region Status Strip Group Base Class
    public abstract class StatusStripGroup
    {
        protected readonly List<BoundStatusLabel> _items = new List<BoundStatusLabel>();

        protected BoundStatusLabel Bind(ToolStripStatusLabel label, string prefix = "", string defaultValue = "")
        {
            var item = new BoundStatusLabel(label, prefix, defaultValue);

            _items.Add(item);

            return item;
        }

        public void ResetAll() => _items.ForEach(i => i.Reset());

        public void ShowAll() => _items.ForEach(i => i.Show());

        public void HideAll() => _items.ForEach(i => i.Hide());

    }
    #endregion

}