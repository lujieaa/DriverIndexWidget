// Geometry and drag/resize: default rectangle, min/max limits, child
// layout, hover-revealed resize zones and the move/resize drag loop.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal enum DragMode
    {
        None, Move, Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

    internal partial class MainForm
    {
        private const int MinW = 200;
        private const int MinH = 140;
        private const int Edge = 8;     // edge hot strip thickness
        private const int Corner = 14;  // corner hot square size
        private const int GripSize = 14;

        private void AttachDrag(Control c, DragMode mode)
        {
            c.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                BeginDrag(mode, c.PointToScreen(e.Location));
            };
            c.MouseMove += DragMouseMove;
            c.MouseUp += EndDrag;
        }

        private void ApplyInitialRect()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int w;
            int h;
            Rectangle r;
            if (_settings.Rect.HasValue)
            {
                Rectangle saved = _settings.Rect.Value;
                w = Clamp(saved.Width, MinW, MaxW());
                h = Clamp(saved.Height, MinH, MaxH());
                int x = Clamp(saved.X, Bounds.Left - w + 80, Bounds.Right - 80);
                int y = Clamp(saved.Y, Bounds.Top, Bounds.Bottom - 44);
                r = new Rectangle(x, y, w, h);
            }
            else
            {
                // Default: 1/5 of the screen wide, 2/3 of the working area
                // tall, parked at the bottom-right corner.
                w = Bounds.Width / 5;
                h = work.Height * 2 / 3;
                r = new Rectangle(work.Right - w - 16, work.Bottom - h - 8, w, h);
            }
            _panel.Bounds = r;
            LayoutChildren();
        }

        private int MaxW()
        {
            return Bounds.Width * 3 / 4;
        }

        private int MaxH()
        {
            return Bounds.Height;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            if (lo > hi) { int t = lo; lo = hi; hi = t; }
            return v < lo ? lo : (v > hi ? hi : v);
        }

        private void LayoutChildren()
        {
            int w = _panel.Width;
            int h = _panel.Height;
            _tree.SetBounds(0, 0, w, h);

            // Zones are addressed via their Tag: L,R,T,B,TL,TR,BL.
            Zone(DragMode.Left).SetBounds(0, 0, Edge, h);
            Zone(DragMode.Right).SetBounds(w - Edge, 0, Edge, h);
            Zone(DragMode.Top).SetBounds(0, 0, w, Edge);
            Zone(DragMode.Bottom).SetBounds(0, h - Edge, w, Edge);
            Zone(DragMode.TopLeft).SetBounds(0, 0, Corner, Corner);
            Zone(DragMode.TopRight).SetBounds(w - Corner, 0, Corner, Corner);
            Zone(DragMode.BottomLeft).SetBounds(0, h - Corner, Corner, Corner);

            // Form-level transparent glyph buttons / blue grip float above
            // the tree. Add and the menu glyph stack vertically at the
            // panel's top-right corner; the grip sits bottom-right.
            int right = _panel.Right - 10;
            int y = _panel.Top + 6;
            int addW = _btnAdd.Width;
            int addH = _btnAdd.Height;
            int menuW = _btnMenu.Width;
            int menuH = _btnMenu.Height;
            _btnMenu.SetBounds(right - menuW, y, menuW, menuH);
            _btnAdd.SetBounds(right - addW, y + menuH + 4, addW, addH);
            _grip.SetBounds(_panel.Right - 6 - GripSize, _panel.Bottom - 6 - GripSize,
                            GripSize, GripSize);
        }

        private Panel Zone(DragMode mode)
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                if ((DragMode)_zones[i].Tag == mode) return _zones[i];
            }
            return null;
        }

        // Light up the resize zone the pointer is close to; zones are the
        // transparency key (invisible, click-through) the rest of the time.
        private void HoverTick()
        {
            if (_drag != DragMode.None) return;
            Point p = Cursor.Position;
            Rectangle r = _panel.RectangleToScreen(_panel.ClientRectangle);
            DragMode z = ZoneAt(p, r);
            if (z == _hoverZone) return;

            if (_hoverZone != DragMode.None && _hoverZone != DragMode.BottomRight)
                SetZoneLit(Zone(_hoverZone), false);
            _hoverZone = z;
            if (z != DragMode.None && z != DragMode.BottomRight)
                SetZoneLit(Zone(z), true);
        }

        private void SetZoneLit(Panel z, bool lit)
        {
            if (z == null) return;
            z.BackColor = lit ? WidgetColors.ResizeHover : WidgetColors.Key;
            z.BringToFront();
        }

        private static DragMode ZoneAt(Point p, Rectangle r)
        {
            int m = Edge + 3;
            bool left = p.X >= r.Left - 1 && p.X <= r.Left + m;
            bool right = p.X <= r.Right + 1 && p.X >= r.Right - m;
            bool top = p.Y >= r.Top - 1 && p.Y <= r.Top + m;
            bool bottom = p.Y <= r.Bottom + 1 && p.Y >= r.Bottom - m;
            bool inside = r.Contains(p);
            if (!inside && !(left || right || top || bottom)) return DragMode.None;

            if (top && left) return DragMode.TopLeft;
            if (top && right) return DragMode.TopRight;
            if (bottom && left) return DragMode.BottomLeft;
            if (bottom && right) return DragMode.BottomRight;
            if (left) return DragMode.Left;
            if (right) return DragMode.Right;
            if (top) return DragMode.Top;
            if (bottom) return DragMode.Bottom;
            return DragMode.None;
        }

        private void BeginDrag(DragMode mode, Point screen)
        {
            _drag = mode;
            _dragStart = screen;
            _dragStartRect = _panel.Bounds;
        }

        private void DragMouseMove(object sender, MouseEventArgs e)
        {
            if (_drag == DragMode.None || e.Button != MouseButtons.Left) return;
            Point now = ((Control)sender).PointToScreen(e.Location);
            ApplyDragDelta(now);
        }

        private void ApplyDragDelta(Point now)
        {
            int dx = now.X - _dragStart.X;
            int dy = now.Y - _dragStart.Y;
            Rectangle s = _dragStartRect;
            Rectangle r = s;

            switch (_drag)
            {
                case DragMode.Move:
                    r.X = s.X + dx;
                    r.Y = s.Y + dy;
                    r.X = Clamp(r.X, Bounds.Left - r.Width + 80, Bounds.Right - 80);
                    r.Y = Clamp(r.Y, Bounds.Top, Bounds.Bottom - 44);
                    break;
                case DragMode.Right:
                    r.Width = Clamp(s.Width + dx, MinW, MaxW());
                    break;
                case DragMode.Left:
                    r.Width = Clamp(s.Width - dx, MinW, MaxW());
                    r.X = s.Right - r.Width;
                    break;
                case DragMode.Bottom:
                    r.Height = Clamp(s.Height + dy, MinH, MaxH());
                    break;
                case DragMode.Top:
                    r.Height = Clamp(s.Height - dy, MinH, MaxH());
                    r.Y = s.Bottom - r.Height;
                    break;
                case DragMode.BottomRight:
                    r.Width = Clamp(s.Width + dx, MinW, MaxW());
                    r.Height = Clamp(s.Height + dy, MinH, MaxH());
                    break;
                case DragMode.TopLeft:
                    r.Width = Clamp(s.Width - dx, MinW, MaxW());
                    r.Height = Clamp(s.Height - dy, MinH, MaxH());
                    r.X = s.Right - r.Width;
                    r.Y = s.Bottom - r.Height;
                    break;
                case DragMode.TopRight:
                    r.Width = Clamp(s.Width + dx, MinW, MaxW());
                    r.Height = Clamp(s.Height - dy, MinH, MaxH());
                    r.Y = s.Bottom - r.Height;
                    break;
                case DragMode.BottomLeft:
                    r.Width = Clamp(s.Width - dx, MinW, MaxW());
                    r.Height = Clamp(s.Height + dy, MinH, MaxH());
                    r.X = s.Right - r.Width;
                    break;
            }
            _panel.Bounds = r;
            LayoutChildren();
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            if (_drag == DragMode.None) return;
            _drag = DragMode.None;
            _settings.Rect = _panel.Bounds;
            _settings.Save();
        }
    }
}
