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

        // Messages after which the native tree re-evaluates and may call
        // ShowScrollBar(TRUE) on itself; we hide the bars again each time.
        private const int WmSetFont = 0x0030;
        private const int WmWindowPosChanged = 0x0047;
        private const int WmSize = 0x0005;
        private const int WmStyleChanged = 0x007D;
        private const int TvmInsertItemA = 0x1100;
        private const int TvmDeleteItem = 0x1100 + 2;
        private const int TvmExpand = 0x1100 + 4;
        private const int TvmInsertItemW = 0x1132;
        private const int TvmSetIndent = 0x1100 + 16;
        private const int TvmEnsureVisible = 0x1100 + 20;
        private const int TvmSetItemHeight = 0x1100 + 23;

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
                cp.Style |= TvsHasButtons | TvsLinesAtRoot;
                // No scroll bars and no white track gutter: the standard
                // scroll-bar windows simply do not exist for this control.
                // TVS_NOSCROLL is deliberately NOT used - it disables not
                // just the bars but also WM_VSCROLL and TVM_ENSUREVISIBLE,
                // which are how wheel ticks scroll the content.
                cp.Style &= ~(Win32.WsVScroll | Win32.WsHScroll);
                return cp;
            }
        }

        // Return true to swallow the press (the host handles up / drag).
        public Func<Point, bool> PressHook;
        public Action<Point> ReleaseHook;
        private bool _captured;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            HideBars();
        }

        private void HideBars()
        {
            Win32.ShowScrollBar(Handle, Win32.SbBoth, false);
        }

        // The native control re-shows its standard scroll bars whenever
        // content or geometry changes. The bars have no WS_xSCROLL window
        // style, so hiding them here (inside the same message dispatch)
        // costs no client width and never flickers.
        private static bool ReshowsBars(int msg)
        {
            switch (msg)
            {
                case WmSize:
                case WmWindowPosChanged:
                case WmSetFont:
                case WmStyleChanged:
                case TvmInsertItemA:
                case TvmInsertItemW:
                case TvmDeleteItem:
                case TvmExpand:
                case TvmEnsureVisible:
                case TvmSetItemHeight:
                case TvmSetIndent:
                    return true;
                default:
                    return false;
            }
        }

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
            if (ReshowsBars(m.Msg)) HideBars();
        }
    }
}
