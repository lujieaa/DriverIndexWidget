// Persisted user choices and panel geometry.
// File: %LOCALAPPDATA%\DDriveIndexWallpaper\settings.txt
//   H|path     hidden folder
//   A|path     pinned (added) folder
//   R|x|y|w|h  tree panel rectangle
//   F|size     tree font size in points (Ctrl + wheel zoom)
//   G|c|r      recent-files grid columns|rows
//   Q|x|y|w|h  recent-files panel rectangle
//   Z|size     recent-files label font size (Ctrl + wheel zoom)
//   U|0/1      tree panel visible
//   V|0/1      recent-files panel visible
//   N|l|c      file name lines | characters per line in grid cells
//   S|n        grid cell inner spacing in pixels
//   X|path     recent file hidden from the grid ("Don't show this file")
//   P|i|path   file pinned to linear grid slot i (0-based, row-major)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace DDriveIndexWallpaper
{
    internal class Settings
    {
        public const float DefaultFontSize = 9.5f;
        public const int DefaultGridCols = 3;
        public const int DefaultGridRows = 2;
        public const int DefaultNameLines = 2;
        public const int DefaultNameChars = 12;
        public const int DefaultCellSpacing = 8;

        public HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Rectangle? Rect;
        public float FontSize = DefaultFontSize; // tree font, Ctrl+wheel zoom

        // Recent-files panel geometry / look / grid shape.
        public int GridCols = DefaultGridCols;
        public int GridRows = DefaultGridRows;
        public Rectangle? RecentRect;
        public float RecentFontSize = DefaultFontSize;
        public bool TreeVisible = true;
        public bool RecentVisible = true;
        public int NameLines = DefaultNameLines;
        public int NameChars = DefaultNameChars;
        public int CellSpacing = DefaultCellSpacing;

        // Recent files the user removed from the grid; pinned files own a
        // linear grid slot (row-major index) and never take MRU fills.
        public HashSet<string> HiddenRecent =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, string> PinnedSlots =
            new Dictionary<int, string>();

        private static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DDriveIndexWallpaper");
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(Dir, "settings.txt"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length < 2 || line[1] != '|') continue;
                    string val = line.Substring(2);
                    if (line.StartsWith("H|", StringComparison.Ordinal)) s.Hidden.Add(val);
                    else if (line.StartsWith("A|", StringComparison.Ordinal)) s.Added.Add(val);
                    else if (line.StartsWith("R|", StringComparison.Ordinal))
                    {
                        string[] p = val.Split('|');
                        int x, y, w, h;
                        if (p.Length == 4 &&
                            int.TryParse(p[0], out x) && int.TryParse(p[1], out y) &&
                            int.TryParse(p[2], out w) && int.TryParse(p[3], out h))
                        {
                            s.Rect = new Rectangle(x, y, w, h);
                        }
                    }
                    else if (line.StartsWith("F|", StringComparison.Ordinal))
                    {
                        float f;
                        if (float.TryParse(val, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out f))
                        {
                            s.FontSize = f;
                        }
                    }
                    else if (line.StartsWith("G|", StringComparison.Ordinal))
                    {
                        string[] p = val.Split('|');
                        int c, r;
                        if (p.Length == 2 &&
                            int.TryParse(p[0], out c) && int.TryParse(p[1], out r))
                        {
                            s.GridCols = ClampGrid(c, true);
                            s.GridRows = ClampGrid(r, false);
                        }
                    }
                    else if (line.StartsWith("Q|", StringComparison.Ordinal))
                    {
                        string[] p = val.Split('|');
                        int x, y, w, h;
                        if (p.Length == 4 &&
                            int.TryParse(p[0], out x) && int.TryParse(p[1], out y) &&
                            int.TryParse(p[2], out w) && int.TryParse(p[3], out h))
                        {
                            s.RecentRect = new Rectangle(x, y, w, h);
                        }
                    }
                    else if (line.StartsWith("Z|", StringComparison.Ordinal))
                    {
                        float f;
                        if (float.TryParse(val, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out f))
                        {
                            s.RecentFontSize = f;
                        }
                    }
                    else if (line.StartsWith("U|", StringComparison.Ordinal))
                    {
                        s.TreeVisible = val != "0";
                    }
                    else if (line.StartsWith("V|", StringComparison.Ordinal))
                    {
                        s.RecentVisible = val != "0";
                    }
                    else if (line.StartsWith("N|", StringComparison.Ordinal))
                    {
                        string[] p = val.Split('|');
                        int l, c;
                        if (p.Length == 2 &&
                            int.TryParse(p[0], out l) && int.TryParse(p[1], out c))
                        {
                            s.NameLines = ClampName(l, true);
                            s.NameChars = ClampName(c, false);
                        }
                    }
                    else if (line.StartsWith("S|", StringComparison.Ordinal))
                    {
                        int sp;
                        if (int.TryParse(val, out sp))
                            s.CellSpacing = ClampSpacing(sp);
                    }
                    else if (line.StartsWith("X|", StringComparison.Ordinal))
                    {
                        if (val.Length > 0) s.HiddenRecent.Add(val);
                    }
                    else if (line.StartsWith("P|", StringComparison.Ordinal))
                    {
                        // '|' is illegal in Windows file paths, so the
                        // first separator splits slot index from the path.
                        int bar = val.IndexOf('|');
                        if (bar > 0)
                        {
                            int slot;
                            string slotText = val.Substring(0, bar);
                            string pinPath = val.Substring(bar + 1);
                            if (int.TryParse(slotText, out slot) &&
                                slot >= 0 && pinPath.Length > 0)
                            {
                                s.PinnedSlots[slot] = pinPath;
                            }
                        }
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                List<string> lines = new List<string>();
                foreach (string p in Hidden) lines.Add("H|" + p);
                foreach (string p in Added) lines.Add("A|" + p);
                if (Rect.HasValue)
                {
                    Rectangle r = Rect.Value;
                    lines.Add("R|" + r.X + "|" + r.Y + "|" + r.Width + "|" + r.Height);
                }
                lines.Add("F|" + FontSize.ToString(CultureInfo.InvariantCulture));
                lines.Add("G|" + GridCols + "|" + GridRows);
                if (RecentRect.HasValue)
                {
                    Rectangle q = RecentRect.Value;
                    lines.Add("Q|" + q.X + "|" + q.Y + "|" + q.Width + "|" + q.Height);
                }
                lines.Add("Z|" + RecentFontSize.ToString(CultureInfo.InvariantCulture));
                lines.Add("U|" + (TreeVisible ? "1" : "0"));
                lines.Add("V|" + (RecentVisible ? "1" : "0"));
                lines.Add("N|" + NameLines + "|" + NameChars);
                lines.Add("S|" + CellSpacing);
                foreach (string p in HiddenRecent) lines.Add("X|" + p);
                List<int> pinSlots = new List<int>(PinnedSlots.Keys);
                pinSlots.Sort();
                foreach (int slot in pinSlots)
                    lines.Add("P|" + slot + "|" + PinnedSlots[slot]);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }

        public static int ClampGrid(int v, bool columns)
        {
            int max = columns ? 12 : 8;
            if (v < 1) return 1;
            if (v > max) return max;
            return v;
        }

        public static int ClampName(int v, bool lines)
        {
            int max = lines ? 4 : 40;
            if (v < 1) return 1;
            if (v > max) return max;
            return v;
        }

        public static int ClampSpacing(int v)
        {
            if (v < 0) return 0;
            if (v > 40) return 40;
            return v;
        }
    }
}
