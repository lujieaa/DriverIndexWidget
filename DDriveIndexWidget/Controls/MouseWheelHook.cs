// WH_MOUSE_LL hook used because the widget never takes keyboard focus
// (WS_EX_NOACTIVATE), so ordinary wheel messages go to whatever window
// the user last focused. While the pointer hovers the panel this hook:
//   - plain wheel      -> scroll the tree (its own scrollbars are hidden)
//   - Ctrl + wheel     -> zoom the tree font
// In both cases the message is swallowed so the window behind does not
// scroll/zoom too. The callback runs on the installing UI thread.
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DDriveIndexWallpaper
{
    internal sealed class MouseWheelHook : IDisposable
    {
        private readonly Func<Rectangle> _hotArea;   // screen rect that triggers
        private readonly Action<int> _onZoom;         // wheel notches, Ctrl held
        private readonly Action<int> _onScroll;       // wheel notches, plain
        private readonly Win32.LowLevelMouseProc _proc;
        private IntPtr _handle;
        private bool _disposed;

        public MouseWheelHook(Func<Rectangle> hotArea,
            Action<int> onZoom, Action<int> onScroll)
        {
            _hotArea = hotArea;
            _onZoom = onZoom;
            _onScroll = onScroll;
            _proc = HookCallback; // keep a reference so the delegate is not GC'd
        }

        public void Start()
        {
            // dwThreadId = 0 / hMod = Zero is valid for low-level hooks.
            _handle = Win32.SetWindowsHookEx(Win32.WhMouseLL, _proc, IntPtr.Zero, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)Win32.WmMouseWheel)
            {
                Win32.PointStruct pt;
                Win32.GetCursorPos(out pt);
                Rectangle area;
                try { area = _hotArea(); }
                catch { area = Rectangle.Empty; }
                if (area.Contains(new Point(pt.X, pt.Y)))
                {
                    Win32.MsllHookStruct data =
                        (Win32.MsllHookStruct)Marshal.PtrToStructure(
                            lParam, typeof(Win32.MsllHookStruct));
                    int delta = (short)((data.MouseData >> 16) & 0xFFFF);
                    int notches = delta / 120;
                    if (notches != 0)
                    {
                        bool ctrl = (Win32.GetAsyncKeyState(Win32.VkControl) & 0x8000) != 0;
                        try
                        {
                            if (ctrl) _onZoom(notches);
                            else _onScroll(notches);
                        }
                        catch { }
                    }
                    return (IntPtr)1; // swallow: keep the panel "modal" to wheel
                }
            }
            return Win32.CallNextHookEx(_handle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_handle != IntPtr.Zero)
            {
                try { Win32.UnhookWindowsHookEx(_handle); }
                catch { }
                _handle = IntPtr.Zero;
            }
        }
    }
}
