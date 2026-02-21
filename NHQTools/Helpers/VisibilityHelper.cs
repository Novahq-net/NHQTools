using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace NHQTools.Helpers
{
    public class VisibilityHelper
    {
        ////////////////////////////////////////////////////////////////////////////////////
        private static readonly Dictionary<Control, bool> _initVisibility = new Dictionary<Control, bool>();
        private static readonly Dictionary<string, HashSet<Control>> _controlGroups = new Dictionary<string, HashSet<Control>>();

        ////////////////////////////////////////////////////////////////////////////////////
        public static void CaptureInitialVisibility(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (!_initVisibility.ContainsKey(c))
                    _initVisibility[c] = c.Visible;

                if (c.HasChildren)
                    CaptureInitialVisibility(c);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void AddToGroup(string groupName, params Control[] controls)
        {
            if (!_controlGroups.ContainsKey(groupName))
                _controlGroups[groupName] = new HashSet<Control>();

            foreach (var c in controls)
            {
                _controlGroups[groupName].Add(c);

                // Ensure we also capture its initial state if we haven't already
                // so ResetToInitial() still works even if we only used AddToGroup
                if (!_initVisibility.ContainsKey(c))
                    _initVisibility[c] = c.Visible;

            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void ToggleVisibility(bool show)
        {
            foreach (var kvp in _initVisibility.Where(kvp => !kvp.Value))
                kvp.Key.Visible = show;

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void ToggleGroup(string groupName, bool visible)
        {
            if (!_controlGroups.TryGetValue(groupName, out var controls) || controls.Count <= 0)
                return;

            foreach (var c in controls)
                c.Visible = visible;

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void FlipGroup(string groupName)
        {
            if (!_controlGroups.TryGetValue(groupName, out var controls) || controls.Count <= 0)
                return;

            // Check state of the first one, flip the rest to match
            var newState = !controls.First().Visible;

            foreach (var c in controls)
                c.Visible = newState;

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static void ResetToInitial()
        {
            foreach (var kvp in _initVisibility)
                kvp.Key.Visible = kvp.Value;
        }
        
    }

}