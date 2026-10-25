// Win32 interop: window styles, hit testing, TreeView messages, capture.
using System;
using System.Runtime.InteropServices;

namespace DDriveIndexWallpaper
{
    internal static class Win32
    {
        // Extended window styles
        public const int WsExToolWindow = 0x00000080;
        public const int WsExNoActivate = 0x08000000;

        // SetWindowPos flags / commands
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoActivate = 0x0010;
        public const int SwRestore = 9;
        public static readonly IntPtr HwndBottom = (IntPtr)1;

        // Hit testing
        public const int WmNcHitTest = 0x0084;
        public const int HtTransparent = -1;

        // Window styles. WS_xSCROLL are stripped from the tree at create
        // time so no scroll bar (or track gutter) ever exists; the tree's
        // internal scroll range still works and honors WM_VSCROLL.
        public const int WsHScroll = 0x00100000;
        public const int WsVScroll = 0x00200000;

        // Scroll commands (programmatic wheel scrolling; TVS_NOSCROLL is
        // NOT used because it disables WM_VSCROLL and TVM_ENSUREVISIBLE).
        public const int WmVScroll = 0x0115;
        public const int SbLineUp = 0;
        public const int SbLineDown = 1;
        public const int SbPageUp = 2;
        public const int SbPageDown = 3;
        public const int SbBoth = 3;

        [DllImport("user32.dll")]
        public static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        // TreeView messages / hit-test flags
        public const int TvmGetItemRect = 0x1100 + 4;
        public const int TvmHitTest = 0x1100 + 17;
        public const uint TvhtNowhere = 0x0001;
        public const uint TvhtOnItemIcon = 0x0002;
        public const uint TvhtOnItemLabel = 0x0004;
        public const uint TvhtOnItemIndent = 0x0008;
        public const uint TvhtOnItemButton = 0x0010;
        public const uint TvhtOnItemRight = 0x0020;
        public const uint TvhtAbove = 0x0100;
        public const uint TvhtBelow = 0x0200;

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref TvRect lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref TvHitTest lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam,
            ref int pvParam, uint fWinIni);

        public const uint SpiGetWheelScrollLines = 0x006C;

        [StructLayout(LayoutKind.Sequential)]
        public struct TvRect
        {
            public IntPtr hItem;
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PointStruct
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TvHitTest
        {
            public PointStruct Pt;
            public uint Flags;
            public IntPtr hItem;
        }

        // ---- low-level mouse hook (for Ctrl+wheel zoom on a no-focus form) ----

        public const int WhMouseLL = 14;
        public const int WmMouseWheel = 0x020A;
        public const int VkControl = 0x11;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out PointStruct lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct MsllHookStruct
        {
            public PointStruct Pt;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr DwExtraInfo;
        }
    }
}
