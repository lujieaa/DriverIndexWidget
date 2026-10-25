// Geometry and drag/resize: default rectangles, min/max limits, child
// layout for both floating panels, hover-revealed resize zones and the
// move/resize drag loop. Every drag carries its owning Panel so one
// shared implementation serves the tree panel and the recent panel.
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

        // The panel currently targeted by an active drag / hover zone.
        private Panel _dragPanel;
        private Panel _hoverPanel;

        private void AttachDrag(Control c, DragMode mode, Panel host)
        {
            c.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                BeginDrag(mode, c.PointToScreen(e.Location), host);
            };
            c.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (_drag == DragMode.None || e.Button != MouseButtons.Left) return;
                Point now = ((Control)s).PointToScreen(e.Location);
                ApplyDragDelta(now, _dragPanel);
            };
            c.MouseUp += EndDrag;
        }

        private void ApplyInitialRects()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;

            // Tree panel: saved rect or bottom-right default.
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

            // Recent panel: saved rect or bottom-left default sized for the
            // default 3 columns x 2 rows grid.
            if (_settings.RecentRect.HasValue)
            {
                Rectangle saved = _settings.RecentRect.Value;
                int rw = Clamp(saved.Width, MinW, MaxW());
                int rh = Clamp(saved.Height, MinH, MaxH());
                int rx = Clamp(saved.X, Bounds.Left - rw + 80, Bounds.Right - 80);
                int ry = Clamp(saved.Y, Bounds.Top, Bounds.Bottom - 44);
                _panelRecent.Bounds = new Rectangle(rx, ry, rw, rh);
            }
            else
            {
                int rw = Bounds.Width / 4;
                if (rw < MinW) rw = MinW;
                int rh = Math.Max(MinH, work.Height / 5);
                _panelRecent.Bounds =
                    new Rectangle(16, work.Bottom - rh - 8, rw, rh);
            }

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
            LayoutTreeChrome();
            if (_panelRecent != null) LayoutRecentChrome();
        }

        private void LayoutTreeChrome()
        {
            int w = _panel.Width;
            int h = _panel.Height;
            _tree.SetBounds(0, 0, w, h);

            // Zones are addressed via their Tag: L,R,T,B,TL,TR,BL.
            TreeZone(DragMode.Left).SetBounds(0, 0, Edge, h);
            TreeZone(DragMode.Right).SetBounds(w - Edge, 0, Edge, h);
            TreeZone(DragMode.Top).SetBounds(0, 0, w, Edge);
            TreeZone(DragMode.Bottom).SetBounds(0, h - Edge, w, Edge);
            TreeZone(DragMode.TopLeft).SetBounds(0, 0, Corner, Corner);
            TreeZone(DragMode.TopRight).SetBounds(w - Corner, 0, Corner, Corner);
            TreeZone(DragMode.BottomLeft).SetBounds(0, h - Corner, Corner, Corner);

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

        private void LayoutRecentChrome()
        {
            int w = _panelRecent.Width;
            int h = _panelRecent.Height;
            _grid.SetBounds(0, 0, w, h);

            RecentZone(DragMode.Left).SetBounds(0, 0, Edge, h);
            RecentZone(DragMode.Right).SetBounds(w - Edge, 0, Edge, h);
            RecentZone(DragMode.Top).SetBounds(0, 0, w, Edge);
            RecentZone(DragMode.Bottom).SetBounds(0, h - Edge, w, Edge);
            RecentZone(DragMode.TopLeft).SetBounds(0, 0, Corner, Corner);
            RecentZone(DragMode.TopRight).SetBounds(w - Corner, 0, Corner, Corner);
            RecentZone(DragMode.BottomLeft).SetBounds(0, h - Corner, Corner, Corner);

            _gripRecent.SetBounds(
                _panelRecent.Right - 6 - GripSize,
                _panelRecent.Bottom - 6 - GripSize,
                GripSize, GripSize);
        }

        private Panel TreeZone(DragMode mode)
        {
            return ZoneIn(_zones, mode);
        }

        private Panel RecentZone(DragMode mode)
        {
            return ZoneIn(_rzones, mode);
        }

        private Panel ZoneIn(Panel[] zones, DragMode mode)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if ((DragMode)zones[i].Tag == mode) return zones[i];
            }
            return null;
        }

        private Panel[] ZonesOf(Panel host)
        {
            return host == _panelRecent ? _rzones : _zones;
        }

        // Light up the resize zone the pointer is close to on either
        // panel; zones stay the transparency key (invisible, click-through)
        // the rest of the time.
        private void HoverTick()
        {
            if (_drag != DragMode.None) return;
            Point p = Cursor.Position;

            Panel foundPanel = null;
            DragMode foundZone = DragMode.None;
            if (_panel.Visible)
            {
                DragMode z = ZoneAt(p, PanelScreenRect(_panel));
                if (z != DragMode.None) { foundPanel = _panel; foundZone = z; }
            }
            if (foundPanel == null && _panelRecent.Visible)
            {
                DragMode z = ZoneAt(p, PanelScreenRect(_panelRecent));
                if (z != DragMode.None) { foundPanel = _panelRecent; foundZone = z; }
            }

            if (foundPanel == _hoverPanel && foundZone == _hoverZone) return;

            if (_hoverPanel != null && _hoverZone != DragMode.BottomRight)
                SetZoneLit(ZoneIn(ZonesOf(_hoverPanel), _hoverZone), false);

            _hoverPanel = foundPanel;
            _hoverZone = foundZone;

            if (foundPanel != null && foundZone != DragMode.BottomRight)
                SetZoneLit(ZoneIn(ZonesOf(foundPanel), foundZone), true);
        }

        private static Rectangle PanelScreenRect(Panel host)
        {
            return host.RectangleToScreen(host.ClientRectangle);
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

        private void BeginDrag(DragMode mode, Point screen, Panel host)
        {
            _drag = mode;
            _dragPanel = host;
            _dragStart = screen;
            _dragStartRect = host.Bounds;
        }

        private void ApplyDragDelta(Point now, Panel host)
        {
            if (host == null) host = _panel;
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
            host.Bounds = r;
            LayoutChildren();
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            if (_drag == DragMode.None) return;
            Panel host = _dragPanel;
            _drag = DragMode.None;
            _dragPanel = null;
            SavePanelRect(host);
        }

        private void SavePanelRect(Panel host)
        {
            if (host == null) return;
            if (host == _panelRecent) _settings.RecentRect = host.Bounds;
            else _settings.Rect = host.Bounds;
            _settings.Save();
        }

        // Pull a panel back inside the screen after a display change.
        private void ClampPanelInside(Panel p)
        {
            Rectangle b = Bounds;
            Rectangle r = p.Bounds;
            if (r.Right <= b.Right - 40 && r.Bottom <= b.Bottom - 20) return;
            r.Width = Clamp(r.Width, MinW, MaxW());
            r.Height = Clamp(r.Height, MinH, MaxH());
            r.X = Clamp(r.X, b.Left - r.Width + 80, b.Right - 80);
            r.Y = Clamp(r.Y, b.Top, b.Bottom - 44);
            p.Bounds = r;
        }
    }
}
