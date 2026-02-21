using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;
using System.Runtime.CompilerServices;

namespace NHQTools.Helpers
{
    public static class ImgButtonHoverHelper
    {

        ////////////////////////////////////////////////////////////////////////////////////
        private class HoverState
        {
            public Image NormalImage;
            public Image HoverImage;
        }

        // Thread-safe table to store state without overwriting the Control.Tag
        // Automatically releases memory when the PictureBox is disposed
        private static readonly ConditionalWeakTable<PictureBox, HoverState> _states = new ConditionalWeakTable<PictureBox, HoverState>();

        ////////////////////////////////////////////////////////////////////////////////////
        public static void Apply(Control parent, string pbNamePrefix, ResourceManager resourceManager)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager), "Resources cannot be null.");

            foreach (Control c in parent.Controls)
            {
                // Check if it's a PictureBox and has the specific prefix
                if (c is PictureBox pb && pb.Name.StartsWith(pbNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // skip if already applied
                    if (_states.TryGetValue(pb, out _))
                        continue;

                    // Read the tag key or fallback to name
                    var tagKey = pb.Tag as string;

                    if (string.IsNullOrWhiteSpace(tagKey))
                        tagKey = pb.Name;

                    // 2. Load the Hover Image
                    var hoverImg = resourceManager.GetObject(tagKey + "-hover") as Image
                                     ?? resourceManager.GetObject(tagKey + "_hover") as Image;

                    // Apply hover state logic if a hover image exists
                    if (hoverImg != null)
                    {
                        var state = new HoverState
                        {
                            NormalImage = pb.Image,
                            HoverImage = hoverImg
                        };

                        // 3. Store state safely and subscribe
                        _states.Add(pb, state);
                        pb.Cursor = Cursors.Hand;
                        pb.MouseEnter += MouseEnter;
                        pb.MouseLeave += MouseLeave;
                    }

                }

                if (c.HasChildren)
                    Apply(c, pbNamePrefix, resourceManager);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Mouse Events
        private static void MouseEnter(object sender, EventArgs e)
        {
            if (sender is PictureBox pb && _states.TryGetValue(pb, out var state))
                pb.Image = state.HoverImage;

        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static void MouseLeave(object sender, EventArgs e)
        {
            if (sender is PictureBox pb && _states.TryGetValue(pb, out var state))
                pb.Image = state.NormalImage;

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Cleanup
        public static void RemoveEvents(PictureBox pb)
        {
            if (pb == null)
                return;

            pb.MouseEnter -= MouseEnter;
            pb.MouseLeave -= MouseLeave;
            pb.Cursor = Cursors.Default;
            _states.Remove(pb);
        }
        #endregion

    }

}