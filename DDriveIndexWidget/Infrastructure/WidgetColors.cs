// Central palette for the floating widget.
using System.Drawing;

namespace DDriveIndexWallpaper
{
    internal static class WidgetColors
    {
        // Chroma key: every pixel of this color is punched out by DWM and
        // becomes fully transparent / click-through. Nothing real is magenta.
        public static readonly Color Key = Color.Magenta;

        // Blue used for the live-update highlight rows.
        public static readonly Color Highlight = Color.FromArgb(41, 115, 200);

        // Dark translucent-looking slate for Add / menu pills.
        public static readonly Color Pill = Color.FromArgb(45, 52, 60);

        // Light blue shown only while the pointer hugs an edge / corner.
        public static readonly Color ResizeHover = Color.FromArgb(130, 180, 245);

        // Soft accent blue for the permanent resize grip: pale at rest,
        // deepening on hover and turning a firm blue while pressed.
        public static readonly Color ResizeGrip = Color.FromArgb(110, 170, 235);
        public static readonly Color ResizeGripHover = Color.FromArgb(74, 144, 226);
        public static readonly Color ResizeGripPressed = Color.FromArgb(31, 92, 170);

        public static readonly Brush HighlightBrush = new SolidBrush(Highlight);
    }
}
