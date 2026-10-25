// A TreeView that never toggles on double click and routes all left
// button presses through hooks. The host decides: quick click on a glyph
// toggles, quick click on a label opens, press-drag anywhere moves the
// whole panel. Mouse is captured so a drag keeps tracking outside.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal class IndexTreeView : TreeView
    {
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const int WmLButtonDblClk = 0x0203;
        private const int TvsHasButtons = 0x0001;  // draw +/- glyphs
        private const int TvsLinesAtRoot = 0x0004; // glyphs on root items too
        private const int TvsNoScroll = 0x2000;     // no scroll bars, ever

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // ShowLines is false for a clean look, which also stops
                // WinForms from emitting TVS_LINESATROOT. Without it root
                // level items get no +/- glyph and never report
                // TVHT_ONITEMBUTTON, so they cannot be expanded. This tree
                // has no synthetic drive root: every visible folder IS a
                // root level item, so both button styles must be forced.
                // TVS_HASLINES stays off, so no connector lines appear.
                cp.Style |= TvsHasButtons | TvsLinesAtRoot | TvsNoScroll;
                return cp;
            }
        }

        // Return true to swallow the press (the host handles up / drag).
        public Func<Point, bool> PressHook;
        public Action<Point> ReleaseHook;
        private bool _captured;

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
