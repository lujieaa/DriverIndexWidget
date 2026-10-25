// Disk enumeration, noise filtering and path helpers for the D: index.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DDriveIndexWallpaper
{
    internal static class FolderScanner
    {
        public const string DriveRoot = @"D:\";

        // Root entries are ordered by "last edit": the newest LastWriteTime
        // of the folder itself or anything inside it (recursive). The full
        // recursive scan runs on a background thread; until it finishes the
        // folder's own LastWriteTime is used as a cheap fallback. Watcher
        // events bump the owning entry live, so an edited folder bubbles to
        // the top without rescanning the whole drive.
        private static readonly Dictionary<string, DateTime> EditTimes =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly object TimeLock = new object();

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

            // Most recently edited folder first; equal times fall back to
            // name order so the list never shuffles nondeterministically.
            result.Sort(delegate(NodeItem a, NodeItem b)
            {
                int c = GetEditTime(b.Path).CompareTo(GetEditTime(a.Path));
                if (c != 0) return c;
                return string.Compare(LeafName(a.Path), LeafName(b.Path),
                    StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        // Cached recursive last-edit time for a root entry. Before the
        // background scan reaches a folder, seed the cache with its own
        // LastWriteTime (one cheap stat per visible folder).
        public static DateTime GetEditTime(string path)
        {
            string key = Normalize(path);
            lock (TimeLock)
            {
                DateTime cached;
                if (EditTimes.TryGetValue(key, out cached)) return cached;
            }

            DateTime own;
            try { own = File.GetLastWriteTimeUtc(path); }
            catch { own = DateTime.MinValue; }

            lock (TimeLock)
            {
                DateTime existing;
                if (!EditTimes.TryGetValue(key, out existing) || own > existing)
                    EditTimes[key] = own;
            }
            return own;
        }

        // Record an edit for every root entry affected by a changed path:
        // the level-2 folder that owns it, plus any pinned folder that is
        // the path itself or contains it.
        public static void BumpEditTime(
            string changedPath, DateTime whenUtc, HashSet<string> pins)
        {
            if (string.IsNullOrEmpty(changedPath)) return;
            string top = TopOwner(changedPath);
            lock (TimeLock)
            {
                if (top != null) SetMaxTime(top, whenUtc);
                if (pins != null)
                {
                    foreach (string pin in pins)
                    {
                        if (SameOrInside(changedPath, pin))
                            SetMaxTime(Normalize(pin), whenUtc);
                    }
                }
            }
        }

        private static void SetMaxTime(string key, DateTime whenUtc)
        {
            DateTime existing;
            if (!EditTimes.TryGetValue(key, out existing) || whenUtc > existing)
                EditTimes[key] = whenUtc;
        }

        // The visible level-2 folder that owns a path (null for a file or
        // folder sitting directly in D:\).
        private static string TopOwner(string path)
        {
            string norm = Normalize(path);
            string root;
            try { root = Path.GetPathRoot(norm); }
            catch { return null; }
            if (!string.Equals(root, DriveRoot, StringComparison.OrdinalIgnoreCase))
                return null;
            string rest = norm.Substring(root.Length).TrimEnd('\\');
            if (rest.Length == 0) return null;
            int slash = rest.IndexOf('\\');
            string first = slash >= 0 ? rest.Substring(0, slash) : rest;
            if (slash < 0) return null; // direct child of D:\ (usually a file)
            return Normalize(root + first);
        }

        // True when path equals folder or lives somewhere below it.
        public static bool SameOrInside(string path, string folder)
        {
            string p = Normalize(path);
            string f = Normalize(folder);
            return string.Equals(p, f, StringComparison.OrdinalIgnoreCase) ||
                   p.StartsWith(f + "\\", StringComparison.OrdinalIgnoreCase);
        }

        // ---- background recursive scan ---------------------------------

        // Walk every visible level-2 folder once, tracking the newest
        // LastWriteTime per root entry (and per pinned deep folder), then
        // merge into the cache. onDone runs on a thread-pool thread.
        public static void WarmUp(
            HashSet<string> pins, HashSet<string> hidden, Action onDone)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<string, DateTime> scan =
                    new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

                foreach (NodeItem top in EnumerateFolders(DriveRoot, hidden))
                {
                    WalkEditTimes(top.Path, Normalize(top.Path), pins, scan);
                }

                // Pinned folders can live inside hidden level-2 trees that
                // the loop above skips; scan those subtrees on their own.
                if (pins != null)
                {
                    foreach (string pin in pins)
                    {
                        string key = Normalize(pin);
                        if (scan.ContainsKey(key)) continue;
                        try { if (!Directory.Exists(pin)) continue; }
                        catch { continue; }
                        WalkEditTimes(pin, key, null, scan);
                    }
                }

                lock (TimeLock)
                {
                    foreach (KeyValuePair<string, DateTime> kv in scan)
                        SetMaxTime(kv.Key, kv.Value);
                }
                if (onDone != null)
                {
                    try { onDone(); }
                    catch { }
                }
            });
        }

        // Fill the recursive edit time of one pinned subtree (used right
        // after a new pin is added).
        public static void WarmSubtree(string rootPath, Action onDone)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<string, DateTime> scan =
                    new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                WalkEditTimes(rootPath, Normalize(rootPath), null, scan);
                lock (TimeLock)
                {
                    foreach (KeyValuePair<string, DateTime> kv in scan)
                        SetMaxTime(kv.Key, kv.Value);
                }
                if (onDone != null)
                {
                    try { onDone(); }
                    catch { }
                }
            });
        }

        private static void WalkEditTimes(string dir, string ownerKey,
            HashSet<string> pins, Dictionary<string, DateTime> scan)
        {
            AccumulateEdit(scan, ownerKey, SafeLastWrite(dir));
            if (pins != null)
            {
                foreach (string pin in pins)
                {
                    if (SameOrInside(dir, pin))
                        AccumulateEdit(scan, Normalize(pin), SafeLastWrite(dir));
                }
            }

            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch { return; }

            foreach (string entry in entries)
            {
                DateTime t = SafeLastWrite(entry);
                AccumulateEdit(scan, ownerKey, t);
                if (pins != null)
                {
                    foreach (string pin in pins)
                    {
                        if (SameOrInside(entry, pin))
                            AccumulateEdit(scan, Normalize(pin), t);
                    }
                }

                FileAttributes a;
                try { a = File.GetAttributes(entry); }
                catch { continue; }
                if ((a & FileAttributes.Directory) != 0 &&
                    (a & FileAttributes.ReparsePoint) == 0)
                {
                    WalkEditTimes(entry, ownerKey, pins, scan);
                }
            }
        }

        private static DateTime SafeLastWrite(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch { return DateTime.MinValue; }
        }

        private static void AccumulateEdit(
            Dictionary<string, DateTime> scan, string key, DateTime t)
        {
            DateTime existing;
            if (!scan.TryGetValue(key, out existing) || t > existing)
                scan[key] = t;
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
