// Recent-files panel: chrome construction, press-drag handling, live
// data (Recent shortcut watcher + polling), diff highlight, Ctrl+wheel
// label zoom, the panel context menu and show/hide plumbing.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal partial class MainForm
    {
        private FileSystemWatcher _recentWatcher;
        private readonly Timer _recentDebounce = new Timer();
        private readonly Timer _recentPoll = new Timer();
        private readonly Timer _recentUnhighlight = new Timer();
        private HashSet<string> _recentPaths; // null until the first load
        private string _menuRecentPath;
        private int _menuRecentIndex = -1;
        private bool _menuRecentPinned;

        private bool _gridPressActive;
        private Point _gridPressPoint;

        private void BuildRecentUi()
        {
            _panelRecent.BackColor = WidgetColors.Key;
            Controls.Add(_panelRecent);

            _grid.Cols = _settings.GridCols;
            _grid.Rows = _settings.GridRows;
            _grid.NameLines = _settings.NameLines;
            _grid.NameChars = _settings.NameChars;
            _grid.Spacing = _settings.CellSpacing;
            _grid.Font = new Font("Segoe UI", _settings.RecentFontSize);
            _grid.PressHook = GridPress;
            _grid.ReleaseHook = GridRelease;
            _grid.MouseDown += GridMouseDown;
            _grid.MouseMove += GridMouseMove;
            _grid.ContextMenuStrip = _recentMenu;
            _panelRecent.Controls.Add(_grid);

            // No top-corner menu glyph on this panel; the same menu opens
            // on right-click anywhere on the grid.
            ToolTip tips = new ToolTip();

            DragMode[] zoneModes =
            {
                DragMode.Left, DragMode.Right, DragMode.Top, DragMode.Bottom,
                DragMode.TopLeft, DragMode.TopRight, DragMode.BottomLeft
            };
            Cursor[] zoneCursors =
            {
                Cursors.SizeWE, Cursors.SizeWE, Cursors.SizeNS, Cursors.SizeNS,
                Cursors.SizeNWSE, Cursors.SizeNESW, Cursors.SizeNESW
            };
            for (int i = 0; i < _rzones.Length; i++)
            {
                Panel z = new Panel();
                z.BackColor = WidgetColors.Key;
                z.Cursor = zoneCursors[i];
                z.Tag = zoneModes[i];
                AttachDrag(z, zoneModes[i], _panelRecent);
                _rzones[i] = z;
                _panelRecent.Controls.Add(z);
            }

            _gripRecent.Text = "";
            _gripRecent.Size = new Size(GripSize, GripSize);
            _gripRecent.FlatStyle = FlatStyle.Flat;
            _gripRecent.FlatAppearance.BorderSize = 0;
            _gripRecent.FlatAppearance.BorderColor = WidgetColors.ResizeGrip;
            _gripRecent.FlatAppearance.MouseOverBackColor = WidgetColors.ResizeGripHover;
            _gripRecent.FlatAppearance.MouseDownBackColor = WidgetColors.ResizeGripPressed;
            _gripRecent.BackColor = WidgetColors.ResizeGrip;
            _gripRecent.UseVisualStyleBackColor = false;
            _gripRecent.Cursor = Cursors.SizeNWSE;
            AttachDrag(_gripRecent, DragMode.BottomRight, _panelRecent);
            Controls.Add(_gripRecent);
            _gripRecent.BringToFront();
            tips.SetToolTip(_gripRecent, "Drag to resize");

            _recentDebounce.Interval = 700;
            _recentDebounce.Tick += delegate
            {
                _recentDebounce.Stop();
                RefreshRecent();
            };
            _recentPoll.Interval = 60000;
            _recentPoll.Tick += delegate { RefreshRecent(); };
            _recentUnhighlight.Interval = 15000;
            _recentUnhighlight.Tick += delegate
            {
                _recentUnhighlight.Stop();
                _grid.SetHighlighted(null);
            };
        }

        // ---- panel visibility (shared with the tray / settings dialog) ----

        private void ApplyPanelVisibility()
        {
            bool tree = _settings.TreeVisible;
            _panel.Visible = tree;
            _btnAdd.Visible = tree;
            _btnMenu.Visible = tree;
            _grip.Visible = tree;

            bool recent = _settings.RecentVisible;
            _panelRecent.Visible = recent;
            _gripRecent.Visible = recent;
        }

        private void ApplyGridShape()
        {
            if (_grid.Cols != _settings.GridCols) _grid.Cols = _settings.GridCols;
            if (_grid.Rows != _settings.GridRows) _grid.Rows = _settings.GridRows;
            if (_grid.NameLines != _settings.NameLines)
                _grid.NameLines = _settings.NameLines;
            if (_grid.NameChars != _settings.NameChars)
                _grid.NameChars = _settings.NameChars;
            if (_grid.Spacing != _settings.CellSpacing)
                _grid.Spacing = _settings.CellSpacing;
        }

        // ---- press vs drag (same contract as the tree) ----

        private bool GridPress(Point p)
        {
            _gridPressActive = true;
            _gridPressPoint = p;
            return true;
        }

        private void GridRelease(Point p)
        {
            bool wasDrag = _drag == DragMode.Move;
            if (wasDrag)
            {
                _drag = DragMode.None;
                SavePanelRect(_panelRecent);
            }
            _gridPressActive = false;
            if (wasDrag) return;

            string path = _grid.PathAt(p);
            if (!string.IsNullOrEmpty(path)) OpenItem(path);
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                RecentFile f = _grid.FileAt(e.Location);
                _menuRecentIndex = _grid.IndexAt(e.Location);
                _menuRecentPath = f != null ? f.Path : null;
                _menuRecentPinned = f != null && f.Pinned;
            }
        }

        private void GridMouseMove(object sender, MouseEventArgs e)
        {
            if (!_gridPressActive) return;
            int dx = e.X - _gridPressPoint.X;
            int dy = e.Y - _gridPressPoint.Y;
            if (_drag != DragMode.Move && (dx * dx + dy * dy) >= 36)
                BeginDrag(DragMode.Move,
                    _grid.PointToScreen(_gridPressPoint), _panelRecent);
            if (_drag == DragMode.Move)
                ApplyDragDelta(_grid.PointToScreen(e.Location), _panelRecent);
        }

        // ---- live data ----

        private void StartRecentWatcher()
        {
            try
            {
                _recentWatcher = new FileSystemWatcher(
                    RecentFiles.RecentDir, "*.lnk");
                _recentWatcher.IncludeSubdirectories = false;
                _recentWatcher.NotifyFilter =
                    NotifyFilters.FileName | NotifyFilters.LastWrite;
                _recentWatcher.Created += delegate { ScheduleRecentRefresh(); };
                _recentWatcher.Changed += delegate { ScheduleRecentRefresh(); };
                _recentWatcher.Deleted += delegate { ScheduleRecentRefresh(); };
                _recentWatcher.Renamed += delegate { ScheduleRecentRefresh(); };
                _recentWatcher.Error += delegate
                {
                    UiInvoke(delegate { StopRecentWatcher(); StartRecentWatcher(); });
                    ScheduleRecentRefresh();
                };
                _recentWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                AppLog.Write("recent watcher start failed: " + ex.Message);
            }
        }

        private void StopRecentWatcher()
        {
            try
            {
                if (_recentWatcher != null)
                {
                    _recentWatcher.EnableRaisingEvents = false;
                    _recentWatcher.Dispose();
                    _recentWatcher = null;
                }
            }
            catch { }
        }

        private void ScheduleRecentRefresh()
        {
            UiInvoke(delegate
            {
                _recentDebounce.Stop();
                _recentDebounce.Start();
            });
        }

        // Resolve shortcuts on a background thread; pinned slots keep
        // their user-chosen files, every other slot fills MRU-first with
        // hidden files and pinned files excluded. Apply on the UI thread
        // and highlight MRU targets new since the previous list.
        private void RefreshRecent()
        {
            int capacity = _settings.GridCols * _settings.GridRows;

            // Snapshot user choices on the UI thread; the scan below runs
            // on a thread pool thread and must not touch _settings.
            Dictionary<int, string> pins =
                new Dictionary<int, string>(_settings.PinnedSlots);
            HashSet<string> hidden = new HashSet<string>(
                _settings.HiddenRecent, StringComparer.OrdinalIgnoreCase);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                RecentFile[] slots = new RecentFile[capacity];
                HashSet<string> pinnedPaths =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int pinnedCount = 0;

                foreach (KeyValuePair<int, string> kv in pins)
                {
                    int slot = kv.Key;
                    string path = kv.Value;
                    if (slot < 0 || slot >= capacity ||
                        string.IsNullOrEmpty(path))
                    {
                        continue;
                    }
                    bool exists = false;
                    DateTime when = DateTime.MinValue;
                    try
                    {
                        exists = File.Exists(path);
                        if (exists) when = File.GetLastWriteTimeUtc(path);
                    }
                    catch { exists = false; }
                    if (!exists) continue; // missing pin: slot takes MRU fill

                    slots[slot] = new RecentFile
                    {
                        Path = path,
                        WhenUtc = when,
                        Pinned = true
                    };
                    pinnedPaths.Add(path);
                    pinnedCount++;
                }

                // Hidden files never fill a slot; pinned files never
                // double-appear in an MRU slot.
                HashSet<string> exclude =
                    new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
                foreach (string p in pinnedPaths) exclude.Add(p);

                int need = capacity - pinnedCount;
                List<RecentFile> pool = need > 0
                    ? RecentFiles.GetRecent(need, exclude)
                    : new List<RecentFile>();

                // MRU pool fills free slots in row-major order; a null
                // tail (pool exhausted) stays as an explicit empty slot.
                int pi = 0;
                for (int i = 0; i < capacity; i++)
                {
                    if (slots[i] == null && pi < pool.Count)
                        slots[i] = pool[pi++];
                }
                List<RecentFile> files = new List<RecentFile>(capacity);
                for (int i = 0; i < capacity; i++) files.Add(slots[i]);

                UiInvoke(delegate
                {
                    if (IsDisposed) return;
                    ApplyGridShape();

                    HashSet<string> nowPaths =
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (RecentFile f in files)
                        if (f != null && !f.Pinned) nowPaths.Add(f.Path);

                    HashSet<string> hit =
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (_recentPaths != null)
                    {
                        foreach (string p in nowPaths)
                            if (!_recentPaths.Contains(p)) hit.Add(p);
                    }

                    _grid.SetFiles(files);
                    _grid.SetHighlighted(hit);
                    _recentPaths = nowPaths;

                    if (hit.Count > 0)
                    {
                        _recentUnhighlight.Stop();
                        _recentUnhighlight.Start();
                    }
                });
            });
        }

        // ---- Ctrl + wheel label / badge zoom ----

        private const float MinGridFontSize = 7f;
        private const float MaxGridFontSize = 22f;

        private void ZoomRecent(int notches)
        {
            if (notches == 0) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { ZoomRecent(notches); }); }
                catch { }
                return;
            }
            float size = _grid.Font.Size + notches * ZoomStep;
            if (size < MinGridFontSize) size = MinGridFontSize;
            if (size > MaxGridFontSize) size = MaxGridFontSize;
            if (Math.Abs(size - _grid.Font.Size) < 0.01f) return;

            Font old = _grid.Font;
            _grid.Font = new Font("Segoe UI", size);
            if (old != null) old.Dispose();
            _grid.Invalidate();

            _settings.RecentFontSize = size;
            _settings.Save();
        }

        // ---- context menu ----

        private void RecentMenuOpening(object sender, CancelEventArgs e)
        {
            _recentMenu.Items.Clear();

            if (!string.IsNullOrEmpty(_menuRecentPath))
            {
                string file = _menuRecentPath;
                int slot = _menuRecentIndex;

                ToolStripMenuItem open = new ToolStripMenuItem("Open");
                open.Click += delegate { OpenItem(file); };
                _recentMenu.Items.Add(open);

                ToolStripMenuItem parent = new ToolStripMenuItem("Open containing folder");
                parent.Click += delegate
                {
                    try { Process.Start("explorer.exe", "/select,\"" + file + "\""); }
                    catch { }
                };
                _recentMenu.Items.Add(parent);
                _recentMenu.Items.Add(new ToolStripSeparator());

                if (_menuRecentPinned)
                {
                    ToolStripMenuItem replace =
                        new ToolStripMenuItem("Replace pinned file...");
                    replace.Click += delegate { PickAndPin(slot); };
                    _recentMenu.Items.Add(replace);

                    ToolStripMenuItem unpin =
                        new ToolStripMenuItem("Unpin this slot");
                    unpin.Click += delegate { UnpinSlot(slot); };
                    _recentMenu.Items.Add(unpin);
                }
                else
                {
                    ToolStripMenuItem pin =
                        new ToolStripMenuItem("Pin a file to this slot...");
                    pin.Click += delegate { PickAndPin(slot); };
                    _recentMenu.Items.Add(pin);

                    ToolStripMenuItem dontShow =
                        new ToolStripMenuItem("Don't show this file");
                    dontShow.Click += delegate { HideRecentFile(file); };
                    _recentMenu.Items.Add(dontShow);
                }
                _recentMenu.Items.Add(new ToolStripSeparator());
            }

            ToolStripMenuItem refresh = new ToolStripMenuItem("Refresh now");
            refresh.Click += delegate { RefreshRecent(); };
            _recentMenu.Items.Add(refresh);

            ToolStripMenuItem settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { ShowSettings(); };
            _recentMenu.Items.Add(settings);

            ToolStripMenuItem hide = new ToolStripMenuItem("Hide this panel");
            hide.Click += delegate
            {
                _settings.RecentVisible = false;
                _settings.Save();
                ApplyPanelVisibility();
            };
            _recentMenu.Items.Add(hide);

            _recentMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { Application.Exit(); };
            _recentMenu.Items.Add(exit);
        }

        // ---- grid slot actions ----

        // Remove one file from the MRU fill order: it joins the persisted
        // hidden set, the list reflows and the newest not-yet-shown file
        // lands in the freed tail slot.
        private void HideRecentFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            _settings.HiddenRecent.Add(path);
            _settings.Save();
            RefreshRecent();
        }

        // Release a pinned slot back to MRU ordering.
        private void UnpinSlot(int slot)
        {
            if (slot < 0) return;
            _settings.PinnedSlots.Remove(slot);
            _settings.Save();
            RefreshRecent();
        }

        // File picker: the chosen file owns the slot permanently. The
        // slot never takes an MRU file until the pin is removed here or
        // via the pinned-slot list in Settings.
        private void PickAndPin(int slot)
        {
            if (slot < 0) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Choose a file to pin to this grid slot";
                dlg.Filter = "All files (*.*)|*.*";
                dlg.CheckFileExists = true;
                if (_menuRecentIndex == slot && !string.IsNullOrEmpty(_menuRecentPath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(_menuRecentPath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            dlg.InitialDirectory = dir;
                    }
                    catch { }
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                _settings.PinnedSlots[slot] = dlg.FileName;
                // A deliberate pin overrides a previous "don't show".
                _settings.HiddenRecent.Remove(dlg.FileName);
                _settings.Save();
                RefreshRecent();
            }
        }
    }
}
