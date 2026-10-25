// A borderless button whose background is the chroma-key color (fully
// see-through). Only the glyph itself is painted: white with a black
// shadow so it stays readable on any wallpaper, turning light blue on
// hover. Note: with a TransparencyKey window, only non-key pixels receive
// mouse hits, so the glyph nearly fills the (auto-sized) bounds.
using System.Drawing;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal class GlyphButton : Button
    {
        private bool _hover;

        public GlyphButton(string glyph)
        {
            Text = glyph;
            BackColor = WidgetColors.Key;
            ForeColor = Color.White;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = WidgetColors.Key;
            FlatAppearance.MouseDownBackColor = WidgetColors.Key;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(5, 3, 5, 3);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            using (SolidBrush bg = new SolidBrush(WidgetColors.Key))
                g.FillRectangle(bg, ClientRectangle);

            Color ink = _hover ? WidgetColors.ResizeHover : Color.White;
            TextFormatFlags f = TextFormatFlags.HorizontalCenter |
                                TextFormatFlags.VerticalCenter |
                                TextFormatFlags.SingleLine;
            Rectangle shadow = ClientRectangle;
            shadow.Offset(1, 1);
            TextRenderer.DrawText(g, Text, Font, shadow,
                Color.FromArgb(190, 0, 0, 0), Color.Transparent, f);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle,
                ink, Color.Transparent, f);
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }
    }
}
