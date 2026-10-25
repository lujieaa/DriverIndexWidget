// Data source for the recent-files grid. Windows maintains per-user
// "Recent Items" as .lnk shortcuts under
// %APPDATA%\Microsoft\Windows\Recent; the shell creates/updates one
// whenever a document is opened or saved from any drive / application.
// Resolving shortcut targets yields recently used files system-wide.
// Enumeration plus COM resolution run on a background thread; targets
// are cached per link path + write time so repeated refreshes are
// cheap when nothing changed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DDriveIndexWallpaper
{
    internal class RecentFile
    {
        public string Path;
        public DateTime WhenUtc;
    }

    internal static class RecentFiles
    {
        public static string RecentDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Recent");
            }
        }

        // key = link path + link write time; value = resolved target
        // (null when the shortcut resolves to nothing usable).
        private static readonly Dictionary<string, string> TargetCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object CacheLock = new object();
        private const int CacheLimit = 3000;

        // Targets that are never documents worth surfacing.
        private static readonly HashSet<string> BlockedExt =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".bat", ".cmd", ".com", ".scr", ".dll",
            ".lnk", ".url", ".ini", ".tmp", ".crdownload", ".sys", ".drv",
            ".appref-ms"
        };

        // Up to max existing files behind the newest recent shortcuts,
        // most recently used first and deduplicated by target path.
        public static List<RecentFile> GetRecent(int max)
        {
            List<RecentFile> result = new List<RecentFile>();
            if (max <= 0) return result;

            string dir = RecentDir;
            string[] links;
            try { links = Directory.GetFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly); }
            catch { return result; }

            Array.Sort(links, delegate(string a, string b)
            {
                DateTime ta, tb;
                try { ta = File.GetLastWriteTimeUtc(a); }
                catch { ta = DateTime.MinValue; }
                try { tb = File.GetLastWriteTimeUtc(b); }
                catch { tb = DateTime.MinValue; }
                return tb.CompareTo(ta);
            });

            object shellObj = null;
            dynamic shell = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                shellObj = Activator.CreateInstance(shellType);
                shell = shellObj;
            }
            catch
            {
                AppLog.Write("recent: WScript.Shell unavailable");
                return result;
            }

            HashSet<string> seen =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string link in links)
                {
                    if (result.Count >= max) break;

                    DateTime when;
                    try { when = File.GetLastWriteTimeUtc(link); }
                    catch { continue; }

                    string target = ResolveTarget(link, when, shell);
                    if (string.IsNullOrEmpty(target)) continue;
                    if (!Acceptable(target, seen, dir)) continue;

                    seen.Add(target);
                    result.Add(new RecentFile { Path = target, WhenUtc = when });
                }
            }
            finally
            {
                try { if (shellObj != null) Marshal.ReleaseComObject(shellObj); }
                catch { }
            }
            return result;
        }

        private static string ResolveTarget(string link, DateTime when, dynamic shell)
        {
            string key = link + "|" + when.Ticks;
            lock (CacheLock)
            {
                string cached;
                if (TargetCache.TryGetValue(key, out cached)) return cached;
            }

            string target = null;
            try
            {
                dynamic sc = shell.CreateShortcut(link);
                try
                {
                    target = sc.TargetPath as string;
                }
                finally
                {
                    try { Marshal.ReleaseComObject(sc); }
                    catch { }
                }
            }
            catch
            {
                target = null;
            }

            if (!string.IsNullOrEmpty(target))
            {
                target = target.Trim().Trim('"');
                if (target.Length == 0) target = null;
            }

            lock (CacheLock)
            {
                if (TargetCache.Count > CacheLimit) TargetCache.Clear();
                TargetCache[key] = target;
            }
            return target;
        }

        private static bool Acceptable(string target, HashSet<string> seen, string recentDir)
        {
            if (seen.Contains(target)) return false;
            try
            {
                if (!File.Exists(target)) return false;
                FileAttributes a = File.GetAttributes(target);
                if ((a & (FileAttributes.Directory | FileAttributes.Hidden |
                          FileAttributes.System | FileAttributes.ReparsePoint)) != 0)
                    return false;
            }
            catch { return false; }

            string name = Path.GetFileName(target);
            if (FolderScanner.IsNoiseFile(name)) return false;

            string ext = Path.GetExtension(target);
            if (BlockedExt.Contains(ext)) return false;

            string parent;
            try { parent = Path.GetDirectoryName(target); }
            catch { parent = null; }
            if (string.Equals(parent, recentDir, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
