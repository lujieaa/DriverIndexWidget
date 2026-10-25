// Context menu (open / hide / add / rescan / exit) and folder
// show/hide filtering backed by Settings.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal partial class MainForm
    {
        private void MenuOpening(object sender, CancelEventArgs e)
        {
            _menu.Items.Clear();
            NodeItem ni = _menuNode == null ? null : _menuNode.Tag as NodeItem;

            if (_menuNode != null && ni != null)
            {
                ToolStripMenuItem open = new ToolStripMenuItem("Open");
                open.Click += delegate { OpenItem(ni.Path); };
                _menu.Items.Add(open);

                if (ni.IsFolder)
                {
                    ToolStripMenuItem hide = new ToolStripMenuItem("Hide from panel");
                    string hidePath = ni.Path;
                    hide.Click += delegate { HideFolder(hidePath); };
                    _menu.Items.Add(hide);
                }
                else
                {
                    ToolStripMenuItem parent = new ToolStripMenuItem("Open containing folder");
                    string file = ni.Path;
                    parent.Click += delegate
                    {
                        try { Process.Start("explorer.exe", "/select,\"" + file + "\""); }
                        catch { }
                    };
                    _menu.Items.Add(parent);
                }
            }
            else
            {
                ToolStripMenuItem add = new ToolStripMenuItem("Add folder...");
                add.Click += delegate { ShowAddDialog(); };
                _menu.Items.Add(add);
                ToolStripMenuItem rescan = new ToolStripMenuItem("Rescan now");
                rescan.Click += delegate { DebounceTick(null, EventArgs.Empty); };
                _menu.Items.Add(rescan);
                ToolStripMenuItem collapse = new ToolStripMenuItem("Collapse tree");
                collapse.Click += delegate { _tree.CollapseAll(); };
                _menu.Items.Add(collapse);
                _menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
                exit.Click += delegate { Application.Exit(); };
                _menu.Items.Add(exit);
            }
        }

        private void HideFolder(string path)
        {
            _settings.Added.Remove(FolderScanner.Normalize(path));
            _settings.Hidden.Add(FolderScanner.Normalize(path));
            _settings.Save();
            RebuildRoot();
        }

        private void ShowAddDialog()
        {
            List<string> hiddenExisting = new List<string>();
            foreach (string p in _settings.Hidden)
            {
                try { if (Directory.Exists(p)) hiddenExisting.Add(p); }
                catch { }
            }
            hiddenExisting.Sort(StringComparer.OrdinalIgnoreCase);

            using (AddForm form = new AddForm(hiddenExisting))
            {
                form.FolderChosen += ShowFolder;
                form.ShowDialog();
            }
        }

        private void ShowFolder(string path)
        {
            string norm = FolderScanner.Normalize(path);
            _settings.Hidden.Remove(norm);
            // Direct level-2 folders need no pin; deeper folders get pinned.
            if (!FolderScanner.IsLevelTwo(norm) && Directory.Exists(path))
                _settings.Added.Add(norm);
            _settings.Save();
            RebuildRoot();
        }
    }
}
