using System.Windows.Forms;

namespace NHQTools.Helpers
{
    public static class AccessibilityTooltipHelper
    {
        // Applies tooltips to controls based on their AccessibleName
        public static ToolTip Apply(Control parent, ToolTip existingToolTip = null)
        {
            // Use the passed ToolTip, or create a new one if none was provided
            var tip = existingToolTip ?? new ToolTip();

            foreach (Control c in parent.Controls)
            {
                // Register the control with the shared ToolTip component
                if (!string.IsNullOrEmpty(c.AccessibleName))
                    tip.SetToolTip(c, c.AccessibleName);

                // Pass the created ToolTip instance down the recursion stack
                if (c.HasChildren)
                    Apply(c, tip);
                
            }

            return tip;
        }

    }

}