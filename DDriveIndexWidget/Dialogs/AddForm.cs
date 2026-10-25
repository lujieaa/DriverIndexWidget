// Borderless dark picker shown by the Add button: re-check hidden folders
// or browse for any D: folder to pin.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal class AddForm : Form
    {
        private readonly CheckedListBox _list = new CheckedListBox();
        private readonly Label _hint = new Label();
        private readonly Button _browse = new Button();
        private readonly Button _close = new Button();

        public event Action<string> FolderChosen;

        public AddForm(IEnumerable<string> hiddenExisting)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(380, 500);
            BackColor = Color.FromArgb(38, 44, 52);
            ShowInTaskbar = false;
            TopMost = true;

            _hint.Text = "Check folders to show on the panel:";
            _hint.ForeColor = Color.White;
            _hint.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _hint.SetBounds(14, 12, 352, 24);
            Controls.Add(_hint);

            _list.SetBounds(14, 42, 352, 390);
            _list.BackColor = Color.FromArgb(29, 34, 41);
            _list.ForeColor = Color.White;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Font = new Font("Segoe UI", 9.5F);
            _list.CheckOnClick = true;
            _list.ItemCheck += ListItemCheck;
            Controls.Add(_list);

            foreach (string p in hiddenExisting)
            {
                string label = Path.GetFileName(p.TrimEnd('\\'));
                if (string.IsNullOrEmpty(label)) label = p;
                _list.Items.Add(new FolderOption(label, p), false);
            }
            if (_list.Items.Count == 0)
            {
                _list.Enabled = false;
                _hint.Text = "All top folders are shown. You can pin any folder:";
            }

            _browse.Text = "Browse folder...";
            _close.Text = "Close";
            ConfigureButton(_browse);
            ConfigureButton(_close);
            _browse.SetBounds(14, 448, 180, 34);
            _close.SetBounds(252, 448, 114, 34);
            _browse.Click += BrowseClick;
            _close.Click += delegate { Close(); };
            Controls.Add(_browse);
            Controls.Add(_close);
        }

        private static void ConfigureButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(47, 115, 200);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 9.5F);
            b.Cursor = Cursors.Hand;
        }

        private void ListItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Fires before the new value is committed; read the new state.
            FolderOption opt = _list.Items[e.Index] as FolderOption;
            if (opt == null) return;
            bool willCheck = e.NewValue == CheckState.Checked;
            if (willCheck && FolderChosen != null) FolderChosen(opt.Path);
        }

        private void BrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pick a folder under D: to show on the panel";
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                dlg.SelectedPath = @"D:\";
                if (dlg.ShowDialog(this) == DialogResult.OK &&
                    !string.IsNullOrEmpty(dlg.SelectedPath))
                {
                    string chosen = dlg.SelectedPath;
                    string root = Path.GetPathRoot(chosen) ?? "";
                    if (!string.Equals(root, @"D:\", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(this, "Only folders on D: can be added.", "D: Drive Index",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (FolderChosen != null) FolderChosen(chosen);
                    Close();
                }
            }
        }

        private class FolderOption
        {
            public readonly string Label;
            public readonly string Path;
            public FolderOption(string label, string path) { Label = label; Path = path; }
            public override string ToString() { return Label; }
        }
    }
}
