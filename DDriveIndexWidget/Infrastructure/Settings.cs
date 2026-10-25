// Persisted user choices and panel geometry.
// File: %LOCALAPPDATA%\DDriveIndexWallpaper\settings.txt
//   H|path     hidden folder
//   A|path     pinned (added) folder
//   R|x|y|w|h  saved panel rectangle
//   F|size     tree font size in points (Ctrl + wheel zoom)
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

        public HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Rectangle? Rect;
        public float FontSize = DefaultFontSize; // tree font, Ctrl+wheel zoom

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
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }
    }
}
