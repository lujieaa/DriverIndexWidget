// Real-time index updates: a recursive FileSystemWatcher on D:, debounced
// refreshes of the expanded branches, diff-based blue highlight + locate.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal partial class MainForm
    {
        private FileSystemWatcher _watcher;

        private void UiInvoke(MethodInvoker action)
        {
            if (IsHandleCreated && !IsDisposed && InvokeRequired)
            {
                try { BeginInvoke(action); }
                catch (Exception ex) { AppLog.Write("UiInvoke failed: " + ex.Message); }
            }
            else
            {
                action();
            }
        }

        private void StartWatcher()
        {
            _watcher = new FileSystemWatcher(FolderScanner.DriveRoot);
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;
            _watcher.InternalBufferSize = 65536;
            _watcher.Created += delegate { ScheduleRefresh(); };
            _watcher.Deleted += delegate { ScheduleRefresh(); };
            _watcher.Renamed += delegate { ScheduleRefresh(); };
            _watcher.Error += delegate(object s, ErrorEventArgs e)
            {
                AppLog.Write("watcher error: " + e.GetException().Message);
                UiInvoke(delegate { RestartWatcher(); ScheduleRefresh(); });
            };
            _watcher.EnableRaisingEvents = true;
        }

        private void RestartWatcher()
        {
            if (IsDisposed) return;
            try
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
            }
            catch { }
            StartWatcher();
        }

        private void ScheduleRefresh()
        {
            UiInvoke(delegate
            {
                _debounce.Stop();
                _debounce.Start();
            });
        }

        private void DebounceTick(object sender, EventArgs e)
        {
            _debounce.Stop();
            EnsureTreeBuilt();
            if (_tree.Nodes.Count == 0) return;

            HashSet<string> before = CollectAllPaths();
            RebuildRoot();
            HashSet<string> after = CollectAllPaths();

            HashSet<string> changed = new HashSet<string>(after, StringComparer.OrdinalIgnoreCase);
            changed.SymmetricExceptWith(before);

            List<TreeNode> hits = new List<TreeNode>();
            HashSet<TreeNode> hitSet = new HashSet<TreeNode>();
            foreach (string p in changed)
            {
                bool existsNow = after.Contains(p);
                TreeNode n = FindInNodes(_tree.Nodes, p);
                if (n == null && !existsNow)
                    n = FindInNodes(_tree.Nodes, FolderScanner.ParentPath(p));
                if (n != null && hitSet.Add(n)) hits.Add(n);
            }
            _highlightedNodes = hits;
            if (hits.Count > 0)
            {
                hits[0].EnsureVisible();
                _unhighlight.Stop();
                _unhighlight.Start();
            }
        }

        // Paths of all currently loaded nodes: the top level itself plus the
        // children of every expanded branch.
        private HashSet<string> CollectAllPaths()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectPaths(_tree.Nodes, set);
            return set;
        }

        private static void CollectPaths(TreeNodeCollection nodes, HashSet<string> set)
        {
            foreach (TreeNode c in nodes)
            {
                NodeItem ni = c.Tag as NodeItem;
                if (ni != null)
                {
                    set.Add(FolderScanner.Normalize(ni.Path));
                    if (c.IsExpanded && !IsUnloaded(c)) CollectPaths(c.Nodes, set);
                }
            }
        }

        private static TreeNode FindInNodes(TreeNodeCollection nodes, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string target = FolderScanner.Normalize(path);
            foreach (TreeNode c in nodes)
            {
                NodeItem ni = c.Tag as NodeItem;
                if (ni == null) continue;
                if (string.Equals(FolderScanner.Normalize(ni.Path), target,
                    StringComparison.OrdinalIgnoreCase)) return c;
                if (c.IsExpanded && !IsUnloaded(c))
                {
                    TreeNode deep = FindInNodes(c.Nodes, path);
                    if (deep != null) return deep;
                }
            }
            return null;
        }
    }
}
