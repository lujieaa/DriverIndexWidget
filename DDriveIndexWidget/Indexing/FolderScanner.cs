// Disk enumeration, noise filtering and path helpers for the D: index.
using System;
using System.Collections.Generic;
using System.IO;

namespace DDriveIndexWallpaper
{
    internal static class FolderScanner
    {
        public const string DriveRoot = @"D:\";

        // Folder names that carry no value in a personal file index.
        private static readonly HashSet<string> IgnoredExact =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$recycle.bin", "recycle.bin", "system volume information",
            "program files", "program files (x86)", "programdata",
            "users", "windows", "recovery", "perflogs", "config.msi",
            "$winreagent", "$sysreset", "$getcurrent", "$windows.~bt", "$windows.~ws",
            "msocache", "documents and settings", "intel", "amd", "onedrivetemp",
            "wpsystem", "wudownloadcache"
        };

        public static bool IsRootNoise(string name)
        {
            return name.StartsWith("program", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNoiseFile(string name)
        {
            string n = name.ToLowerInvariant();
            return n == "desktop.ini" || n == "thumbs.db" ||
                   n.EndsWith(".tmp") || n.EndsWith(".crdownload");
        }

        // Strip trailing backslashes; the drive root itself stays "D:\".
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.Length <= 3) return DriveRoot;
            return path.TrimEnd('\\');
        }

        public static string LeafName(string path)
        {
            string p = path.TrimEnd('\\');
            int i = p.LastIndexOf('\\');
            return i >= 0 ? p.Substring(i + 1) : p;
        }

        public static string ParentPath(string path)
        {
            string p = path.TrimEnd('\\');
            int i = p.LastIndexOf('\\');
            if (i <= 2) return DriveRoot;
            return i >= 0 ? p.Substring(0, i) : null;
        }

        public static bool IsLevelTwo(string path)
        {
            string parent = Path.GetDirectoryName(Normalize(path));
            return string.Equals(
                parent == null ? "" : parent.TrimEnd('\\'),
                "D:", StringComparison.OrdinalIgnoreCase);
        }

        // Entries shown at the tree root: visible level-2 folders plus pins.
        public static List<NodeItem> GetRootEntries(
            HashSet<string> hidden, HashSet<string> added)
        {
            List<NodeItem> result = new List<NodeItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (NodeItem item in EnumerateFolders(DriveRoot, hidden))
            {
                result.Add(item);
                seen.Add(Normalize(item.Path));
            }

            List<string> pins = new List<string>(added);
            pins.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string pin in pins)
            {
                string norm = Normalize(pin);
                if (seen.Contains(norm) || hidden.Contains(norm)) continue;
                try
                {
                    if (!Directory.Exists(pin)) continue;
                    FileAttributes a = File.GetAttributes(pin);
                    if ((a & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                result.Add(new NodeItem { Path = pin, IsFolder = true });
                seen.Add(norm);
            }
            return result;
        }

        public static List<NodeItem> EnumerateFolders(string path, HashSet<string> hidden)
        {
            bool rootLevel = path.TrimEnd('\\').Length <= 2;
            List<NodeItem> dirs = new List<NodeItem>();
            try
            {
                foreach (string d in Directory.EnumerateDirectories(path))
                {
                    string name = Path.GetFileName(d);
                    if (FolderFiltered(name, rootLevel)) continue;
                    if (hidden != null && hidden.Contains(Normalize(d))) continue;
                    if (!IsVisibleFilesystemEntry(d)) continue;
                    dirs.Add(new NodeItem { Path = d, IsFolder = true });
                }
            }
            catch { }
            return dirs;
        }

        // Folders first (sorted), then files (sorted); noise and hidden
        // / system / reparse entries are skipped.
        public static List<NodeItem> EnumerateChildren(string path, HashSet<string> hidden)
        {
            bool rootLevel = path.TrimEnd('\\').Length <= 2;
            List<NodeItem> dirs = new List<NodeItem>();
            List<NodeItem> files = new List<NodeItem>();

            try
            {
                foreach (string d in Directory.EnumerateDirectories(path))
                {
                    string name = Path.GetFileName(d);
                    if (FolderFiltered(name, rootLevel)) continue;
                    if (hidden != null && hidden.Contains(Normalize(d))) continue;
                    if (!IsVisibleFilesystemEntry(d)) continue;
                    dirs.Add(new NodeItem { Path = d, IsFolder = true });
                }
            }
            catch { }

            try
            {
                foreach (string f in Directory.EnumerateFiles(path))
                {
                    string name = Path.GetFileName(f);
                    if (IsNoiseFile(name)) continue;
                    if (!IsVisibleFilesystemEntry(f)) continue;
                    files.Add(new NodeItem { Path = f, IsFolder = false });
                }
            }
            catch { }

            Comparison<NodeItem> cmp = delegate(NodeItem a, NodeItem b)
            {
                return string.Compare(LeafName(a.Path), LeafName(b.Path),
                    StringComparison.OrdinalIgnoreCase);
            };
            dirs.Sort(cmp);
            files.Sort(cmp);
            dirs.AddRange(files);
            return dirs;
        }

        private static bool FolderFiltered(string name, bool rootLevel)
        {
            if (IgnoredExact.Contains(name)) return true;
            if (rootLevel && IsRootNoise(name)) return true;
            return false;
        }

        private static bool IsVisibleFilesystemEntry(string path)
        {
            FileAttributes a;
            try { a = File.GetAttributes(path); }
            catch { return false; }
            if ((a & FileAttributes.ReparsePoint) != 0) return false;
            if ((a & FileAttributes.Hidden) != 0 ||
                (a & FileAttributes.System) != 0) return false;
            return true;
        }
    }
}
