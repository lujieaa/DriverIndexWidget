// Owner-drawn grid of recently used files. The visual language mirrors
// IndexTreeView: a chroma-key transparent surface, white text with a
// black shadow, and the same solid blue bar for live newcomers.
//
// Every cell is inset by Spacing pixels ("gap inside/around every cell"
// in the settings). Inside that content rect the file type's REAL shell
// icon sits on the left, the file name on the right.
//
// SIZING IS ZOOM-DRIVEN. Ctrl+wheel sets the base font (persisted as
// RecentFontSize); the rendered label font IS that base - it is never
// re-fitted downward against the longest name, which used to pin every
// cell to one width-limited size so zoom changed nothing. The icon
// square is NameLines base line-heights, clamped only by the cell's own
// height/width so it always tracks the wheel.
//
// Long names adapt to bigger zoom instead of shrinking the whole grid:
// the name is hard-capped at NameLines x NameChars characters, spread
// evenly across NameLines lines at rest, then every line is re-wrapped
// by a WIDTH budget (full-width glyph = 2 units, the rest = 1) measured
// for the current font - bigger zoom means fewer glyphs per line and
// more lines. The line count is capped by cell height; anything still
// overflowing ends on an ellipsised last line (the tooltip already
// shows the full path), exactly like Explorer desktop icons.
//
// Left presses are routed through hooks exactly like IndexTreeView:
// quick release opens the file under the cursor, press-drag moves the
// whole panel. Right clicks are left to the host for the context menu.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal class RecentGrid : Control
    {
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const int WmLButtonDblClk = 0x0203;
        private const int MinTextW = 36;   // keep a readable text column
        private const string Ellipsis = "\u2026";

        private int _cols = 3;
        private int _rows = 2;
        private int _nameLines = 2;
        private int _nameChars = 12;
        private int _spacing = 8;
        private List<RecentFile> _files = new List<RecentFile>();
        private HashSet<string> _highlighted =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Func<Point, bool> PressHook;
        public Action<Point> ReleaseHook;

        private bool _captured;
        private readonly ToolTip _tips = new ToolTip();
        private int _tipIndex = -1;

        // Zoom-driven uniform font + icon side, valid for one geometry
        // (cell size buckets, wrap settings, spacing, zoom base, files).
        private Font _uniformFont;
        private int _uniformIconH;
        private int _layBucketW = int.MinValue;
        private int _layBucketH = int.MinValue;
        private int _layNameLines = -1;
        private int _layNameChars = -1;
        private int _laySpacing = -1;
        private float _layBaseSize = -1f;
        private int _layFilesSig = int.MinValue;
        private int _filesSig;

        // Width-reflowed, height-capped lines per displayed name.
        private sealed class CellLines
        {
            public List<string> Lines;
        }

        private readonly Dictionary<string, CellLines> _lineCache =
            new Dictionary<string, CellLines>(StringComparer.Ordinal);

        public RecentGrid()
        {
            BackColor = WidgetColors.Key;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                     true);
        }

        public int Cols
        {
            get { return _cols; }
            set { _cols = value < 1 ? 1 : value; Invalidate(); }
        }

        public int Rows
        {
            get { return _rows; }
            set { _rows = value < 1 ? 1 : value; Invalidate(); }
        }

        // File name layout: up to Lines x Chars characters, spread
        // evenly across Lines at rest and re-wrapped by width on zoom.
        public int NameLines
        {
            get { return _nameLines; }
            set { _nameLines = value < 1 ? 1 : value; Invalidate(); }
        }

        public int NameChars
        {
            get { return _nameChars; }
            set { _nameChars = value < 1 ? 1 : value; Invalidate(); }
        }

        // Inset of every cell's content (icon + label), in pixels.
        public int Spacing
        {
            get { return _spacing; }
            set { _spacing = value < 0 ? 0 : value; Invalidate(); }
        }

        private void InvalidateLayout()
        {
            if (_uniformFont != null)
            {
                _uniformFont.Dispose();
                _uniformFont = null;
            }
            _layBucketW = int.MinValue;
            _layBucketH = int.MinValue;
            _lineCache.Clear();
        }

        // The list index IS the linear slot index; null entries are empty
        // slots (a missing pin target with the MRU pool exhausted) and
        // stay blank instead of shifting the following cells left.
        public void SetFiles(List<RecentFile> files)
        {
            _files = files ?? new List<RecentFile>();
            int sig = _files.Count;
            for (int i = 0; i < _files.Count; i++)
            {
                if (_files[i] == null)
                {
                    sig = sig * 31;
                    continue;
                }
                string n = Path.GetFileName(_files[i].Path) ?? "";
                int wide = 0;
                for (int k = 0; k < n.Length; k++)
                    if (IsWideGlyph(n[k])) wide++;
                sig = sig * 31 + n.Length;
                sig = sig * 31 + wide;
                sig = sig * 31 + (_files[i].Pinned ? 1 : 0);
            }
            _filesSig = sig;
            InvalidateLayout();
            Invalidate();
        }

        public void SetHighlighted(HashSet<string> paths)
        {
            _highlighted = paths ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Invalidate();
        }

        public int IndexAt(Point p)
        {
            int w = ClientSize.Width;
            int hgt = ClientSize.Height;
            if (_cols <= 0 || _rows <= 0 || w <= 0 || hgt <= 0) return -1;
            if (p.X < 0 || p.Y < 0 || p.X >= w || p.Y >= hgt) return -1;
            int cw = w / _cols;
            int ch = hgt / _rows;
            if (cw <= 0 || ch <= 0) return -1;
            int col = p.X / cw;
            int row = p.Y / ch;
            if (col < 0 || col >= _cols || row < 0 || row >= _rows) return -1;
            int index = row * _cols + col;
            return index < _files.Count ? index : -1;
        }

        public string PathAt(Point p)
        {
            RecentFile f = FileAt(p);
            return f != null ? f.Path : null;
        }

        // Null for empty areas and empty slots.
        public RecentFile FileAt(Point p)
        {
            int i = IndexAt(p);
            return (i >= 0 && i < _files.Count) ? _files[i] : null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(WidgetColors.Key))
                g.FillRectangle(bg, ClientRectangle);

            int w = ClientSize.Width;
            int hgt = ClientSize.Height;
            if (_cols <= 0 || _rows <= 0 || w <= 0 || hgt <= 0) return;

            int cw = w / _cols;
            int ch = hgt / _rows;
            int sp = _spacing;

            int contentW = cw - 2 * sp;
            int contentH = ch - 2 * sp;
            if (contentW < 8) contentW = 8;
            if (contentH < 8) contentH = 8;

            EnsureUniform(contentW, contentH, sp);
            Font label = _uniformFont;
            int lineH = label.Height;
            int iconH = _uniformIconH;

            TextFormatFlags lineFlags =
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding;

            for (int i = 0; i < _files.Count && i < _cols * _rows; i++)
            {
                RecentFile rf = _files[i];
                if (rf == null) continue; // empty slot keeps its position

                int col = i % _cols;
                int row = i / _cols;
                Rectangle cell = new Rectangle(col * cw, row * ch, cw, ch);

                Rectangle content = new Rectangle(
                    cell.X + sp, cell.Y + sp, cw - 2 * sp, ch - 2 * sp);
                if (content.Width < 8) content.Width = 8;
                if (content.Height < 8) content.Height = 8;

                if (_highlighted.Contains(rf.Path))
                    g.FillRectangle(WidgetColors.HighlightBrush, content);

                int textX = content.X + iconH + sp;
                int textW = content.Right - textX;
                if (textW < 6) textW = 6;
                int maxLines = (content.Height - 2) / lineH;
                if (maxLines < 1) maxLines = 1;

                List<string> lines = LinesFor(g, label,
                    Path.GetFileName(rf.Path), textW, maxLines, lineFlags);

                // Icon block is NameLines line-heights; text may use more
                // (re-wrapped) or fewer (short name) lines, centred.
                int textH = lines.Count * lineH;
                int blockH = Math.Max(iconH, textH);
                int top = content.Y + (content.Height - blockH) / 2;

                Rectangle iconRect = new Rectangle(
                    content.X, top + (blockH - iconH) / 2, iconH, iconH);
                DrawShellIcon(g, rf.Path, iconRect);

                int textTop = top + (blockH - textH) / 2;
                for (int k = 0; k < lines.Count; k++)
                {
                    Rectangle slot = new Rectangle(
                        textX, textTop + k * lineH, textW, lineH);
                    Rectangle shadow = slot;
                    shadow.Offset(1, 1);
                    TextRenderer.DrawText(g, lines[k], label, shadow,
                        Color.FromArgb(190, 0, 0, 0), Color.Transparent, lineFlags);
                    TextRenderer.DrawText(g, lines[k], label, slot,
                        Color.White, Color.Transparent, lineFlags);
                }

                // Small white dot marks a user-pinned slot.
                if (rf.Pinned)
                {
                    try
                    {
                        int d = Math.Max(4, content.Width / 18);
                        Rectangle dot = new Rectangle(
                            content.Right - d - 1, content.Y + 1, d, d);
                        using (SolidBrush pin = new SolidBrush(
                            Color.FromArgb(235, 255, 255, 255)))
                        {
                            g.FillEllipse(pin, dot);
                        }
                    }
                    catch { }
                }
            }
        }

        // Zoom-driven uniform sizing: label font equals the zoom base,
        // icon side is NameLines line-heights clamped to the cell. No
        // downward fit against long names - those reflow/ellipsise.
        private void EnsureUniform(int contentW, int contentH, int sp)
        {
            int bw = contentW / 8;
            int bh = contentH / 8;
            if (_uniformFont != null &&
                bw == _layBucketW && bh == _layBucketH &&
                _nameLines == _layNameLines && _nameChars == _layNameChars &&
                sp == _laySpacing &&
                Math.Abs(_layBaseSize - Font.Size) <= 0.01f &&
                _filesSig == _layFilesSig)
            {
                return;
            }

            Font font = new Font(Font.FontFamily, Font.Size, Font.Style);
            int iconH = font.Height * _nameLines;

            int maxH = contentH - 2;
            int maxW = contentW - sp - MinTextW;
            if (iconH > maxH) iconH = maxH;
            if (iconH > maxW) iconH = maxW;
            if (iconH < 8) iconH = 8;

            if (_uniformFont != null) _uniformFont.Dispose();
            _uniformFont = font;
            _uniformIconH = iconH;
            _layBucketW = bw;
            _layBucketH = bh;
            _layNameLines = _nameLines;
            _layNameChars = _nameChars;
            _laySpacing = sp;
            _layBaseSize = Font.Size;
            _layFilesSig = _filesSig;
            _lineCache.Clear();
        }

        // Cap -> evenly spread over NameLines -> width reflow -> cap by
        // cell height with an ellipsised tail. Results are cached per
        // name for the current geometry/font (cache cleared on refit).
        private List<string> LinesFor(Graphics g, Font font, string name,
            int textW, int maxLines, TextFormatFlags flags)
        {
            string key = name ?? "";
            CellLines hit;
            if (_lineCache.TryGetValue(key, out hit)) return hit.Lines;

            string capped = CapName(key, _nameLines * _nameChars);

            // Half-em units: a CJK glyph costs 2, a plain glyph 1.
            int adv = TextRenderer.MeasureText(
                g, "\u4E2D", font, Size.Empty, flags).Width;
            if (adv < 1) adv = 1;
            int units = (int)Math.Floor(2.0 * textW / adv);
            if (units < 2) units = 2;

            List<string> lines = new List<string>();
            List<string> canonical = DistributeEvenly(capped, _nameLines);
            for (int i = 0; i < canonical.Count; i++)
                lines.AddRange(WrapUnits(canonical[i], units));

            if (lines.Count > maxLines)
            {
                List<string> cut = new List<string>();
                for (int i = 0; i < maxLines; i++) cut.Add(lines[i]);
                cut[maxLines - 1] = Ellipsise(g, font, cut[maxLines - 1],
                    textW, adv, flags);
                lines = cut;
            }

            CellLines result = new CellLines();
            result.Lines = lines;
            _lineCache[key] = result;
            return lines;
        }

        // Trim characters until the line + ellipsis fits the pixel width.
        private static string Ellipsise(Graphics g, Font font, string line,
            int textW, int cjkAdv, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(line)) return Ellipsis;
            string candidate = line + Ellipsis;
            while (candidate.Length > 1 &&
                   TextRenderer.MeasureText(g, candidate, font, Size.Empty, flags).Width > textW)
            {
                int cut = candidate.Length - 2; // keep the appended ellipsis
                if (cut > 0 && char.IsLowSurrogate(candidate[cut - 1]) &&
                    char.IsHighSurrogate(candidate[cut - 2]))
                {
                    cut--;
                }
                if (cut < 0) cut = 0;
                candidate = candidate.Substring(0, cut) + Ellipsis;
            }
            return candidate;
        }

        private static void DrawShellIcon(Graphics g, string path, Rectangle rect)
        {
            Bitmap bmp = null;
            try { bmp = FileIcons.Get(path); }
            catch { }

            if (bmp != null)
            {
                // DrawIconEx silently fails on this machine; GDI+ scaling
                // of the cached large-icon bitmap renders correctly.
                System.Drawing.Drawing2D.InterpolationMode oldInterp =
                    g.InterpolationMode;
                System.Drawing.Drawing2D.PixelOffsetMode oldOffset =
                    g.PixelOffsetMode;
                g.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode =
                    System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                try { g.DrawImage(bmp, rect); }
                catch { }
                finally
                {
                    g.InterpolationMode = oldInterp;
                    g.PixelOffsetMode = oldOffset;
                }
            }
            else
            {
                // Last-resort white hollow square so layout stays legible.
                try
                {
                    using (Pen p = new Pen(Color.White))
                        g.DrawRectangle(p, rect);
                }
                catch { }
            }
        }

        // Hard cap on displayed characters. A trailing high surrogate is
        // carried into the cut so a surrogate pair is never sliced in half.
        private static string CapName(string name, int maxChars)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (name.Length <= maxChars) return name;
            int cut = maxChars;
            if (cut < name.Length && char.IsHighSurrogate(name[cut - 1]) &&
                char.IsLowSurrogate(name[cut]))
            {
                cut++;
            }
            return name.Substring(0, cut);
        }

        // Spread text across at most lineCount lines as evenly as
        // possible: line lengths differ by at most one and the first
        // lines receive the extra characters (13 over 3 -> 5+4+4).
        // Fewer characters than lines use one character per line.
        // Surrogate pairs stay whole at the cut points.
        private static List<string> DistributeEvenly(string text, int lineCount)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add("");
                return result;
            }
            if (lineCount <= 1)
            {
                result.Add(text);
                return result;
            }

            int n = text.Length;
            int per = n / lineCount;
            int extra = n % lineCount;
            if (per == 0)
            {
                // Fewer characters than requested lines.
                for (int i = 0; i < n; i++)
                    result.Add(text.Substring(i, 1));
                return result;
            }

            int pos = 0;
            for (int i = 0; i < lineCount; i++)
            {
                int take = per + (i < extra ? 1 : 0);
                if (pos + take > n) take = n - pos;
                // Do not sever a surrogate pair at the cut.
                if (take > 0 && pos + take < n &&
                    char.IsHighSurrogate(text[pos + take - 1]) &&
                    char.IsLowSurrogate(text[pos + take]))
                {
                    take++;
                }
                if (take > 0 && pos < n)
                {
                    result.Add(text.Substring(pos, take));
                    pos += take;
                }
            }
            return result;
        }

        // Wrap by display-width units (full-width = 2, others = 1).
        // Surrogate pairs count as one full-width glyph and stay whole.
        private static List<string> WrapUnits(string text, int unitsPerLine)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add("");
                return result;
            }

            int start = 0;
            int units = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool pair = char.IsHighSurrogate(c) && i + 1 < text.Length &&
                            char.IsLowSurrogate(text[i + 1]);
                int w = (pair || IsWideGlyph(c)) ? 2 : 1;

                if (units > 0 && units + w > unitsPerLine)
                {
                    result.Add(text.Substring(start, i - start));
                    start = i;
                    units = 0;
                }
                units += w;
                if (pair) i++;
            }
            result.Add(text.Substring(start));
            return result;
        }

        private static bool IsWideGlyph(char c)
        {
            // Ranges that render at the full-width CJK cell. Mirrors the
            // tree's label budget; keep both in sync.
            return (c >= 0x1100 && c <= 0x115F) ||
                   (c >= 0x2E80 && c <= 0x303E) ||
                   (c >= 0x3041 && c <= 0x33FF) ||
                   (c >= 0x3400 && c <= 0x4DBF) ||
                   (c >= 0x4E00 && c <= 0x9FFF) ||
                   (c >= 0xA000 && c <= 0xA4CF) ||
                   (c >= 0xAC00 && c <= 0xD7A3) ||
                   (c >= 0xF900 && c <= 0xFAFF) ||
                   (c >= 0xFE30 && c <= 0xFE4F) ||
                   (c >= 0xFF00 && c <= 0xFF60) ||
                   (c >= 0xFFE0 && c <= 0xFFE6);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            RecentFile f = FileAt(e.Location);
            Cursor = f != null ? Cursors.Hand : Cursors.Default;
            int i = IndexAt(e.Location);
            if (i != _tipIndex)
            {
                _tipIndex = i;
                _tips.SetToolTip(this, f != null ? f.Path : "");
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            InvalidateLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                InvalidateLayout();
                _tips.Dispose();
            }
            base.Dispose(disposing);
        }

        // Press capture / release routing, same contract as IndexTreeView.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmLButtonDblClk)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WmLButtonDown && PressHook != null)
            {
                int x = (short)(m.LParam.ToInt64() & 0xFFFF);
                int y = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                if (PressHook(new Point(x, y)))
                {
                    Win32.SetCapture(Handle);
                    _captured = true;
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            if (m.Msg == WmLButtonUp && _captured)
            {
                int x = (short)(m.LParam.ToInt64() & 0xFFFF);
                int y = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                _captured = false;
                Win32.ReleaseCapture();
                m.Result = IntPtr.Zero;
                if (ReleaseHook != null) ReleaseHook(new Point(x, y));
                return;
            }
            base.WndProc(ref m);
        }
    }
}
