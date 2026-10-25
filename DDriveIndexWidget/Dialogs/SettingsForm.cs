// Shared settings dialog: recent-files grid shape (columns x rows),
// file name wrapping (lines, max chars per line, even distribution),
// cell spacing and per-panel visibility. Borderless dark card.
//
// All layout coordinates are authored at 96 DPI and multiplied by the
// system DPI factor through R(); fonts are point-sized and scale by
// themselves. Without this the dialog crammed together at 150%/200%.
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal class SettingsForm : Form
    {
        private readonly float _s;
        private readonly NumericUpDown _cols = new NumericUpDown();
        private readonly NumericUpDown _rows = new NumericUpDown();
        private readonly NumericUpDown _nameLines = new NumericUpDown();
        private readonly NumericUpDown _nameChars = new NumericUpDown();
        private readonly NumericUpDown _spacing = new NumericUpDown();
        private readonly Label _info = new Label();
        private readonly CheckBox _showTree = new CheckBox();
        private readonly CheckBox _showRecent = new CheckBox();
        private readonly Panel _pinHost = new Panel();
        private readonly ToolTip _pinTips = new ToolTip();
        private readonly List<KeyValuePair<int, string>> _pinsLive;
        private readonly HashSet<int> _removedPins = new HashSet<int>();

        public int GridColumns { get { return (int)_cols.Value; } }
        public int GridRows { get { return (int)_rows.Value; } }
        public int NameLines { get { return (int)_nameLines.Value; } }
        public int NameChars { get { return (int)_nameChars.Value; } }
        public int CellSpacing { get { return (int)_spacing.Value; } }
        public bool ShowTree { get { return _showTree.Checked; } }
        public bool ShowRecent { get { return _showRecent.Checked; } }

        // Slot indexes whose pin was removed in this dialog session;
        // applied by the owner only when Save is pressed.
        public IEnumerable<int> RemovedPins { get { return _removedPins; } }

        // Scale a 96-DPI design coordinate to the current system DPI.
        private int R(int v) { return (int)Math.Round(v * _s); }

        public SettingsForm(int columns, int gridRows, int nameLines,
            int nameChars, int spacing, bool treeVisible, bool recentVisible,
            IList<KeyValuePair<int, string>> pinned)
        {
            using (Graphics gx = Graphics.FromHwnd(IntPtr.Zero))
                _s = gx.DpiX / 96f;

            _pinsLive = pinned != null
                ? new List<KeyValuePair<int, string>>(pinned)
                : new List<KeyValuePair<int, string>>();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(R(360), R(508));
            BackColor = Color.FromArgb(38, 44, 52);
            ShowInTaskbar = false;
            TopMost = true;

            Label title = new Label();
            title.Text = "Settings";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            title.SetBounds(R(16), R(12), R(328), R(26));
            Controls.Add(title);

            Label group = new Label();
            group.Text = "Recent files grid (columns x rows):";
            group.ForeColor = Color.White;
            group.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            group.SetBounds(R(16), R(46), R(328), R(22));
            Controls.Add(group);

            Label colLabel = MakeLabel("Columns:");
            colLabel.SetBounds(R(16), R(76), R(66), R(22));
            Controls.Add(colLabel);

            ConfigureNumeric(_cols);
            _cols.Minimum = 1;
            _cols.Maximum = 12;
            _cols.Value = Settings.ClampGrid(columns, true);
            _cols.SetBounds(R(86), R(73), R(64), R(26));
            _cols.ValueChanged += delegate { UpdateInfo(); };
            Controls.Add(_cols);

            Label rowLabel = MakeLabel("Rows:");
            rowLabel.SetBounds(R(178), R(76), R(44), R(22));
            Controls.Add(rowLabel);

            ConfigureNumeric(_rows);
            _rows.Minimum = 1;
            _rows.Maximum = 8;
            _rows.Value = Settings.ClampGrid(gridRows, false);
            _rows.SetBounds(R(226), R(73), R(64), R(26));
            _rows.ValueChanged += delegate { UpdateInfo(); };
            Controls.Add(_rows);

            _info.ForeColor = Color.FromArgb(170, 200, 235);
            _info.Font = new Font("Segoe UI", 9F);
            _info.SetBounds(R(16), R(104), R(328), R(20));
            Controls.Add(_info);
            UpdateInfo();

            Label nameGroup = new Label();
            nameGroup.Text = "File name text:";
            nameGroup.ForeColor = Color.White;
            nameGroup.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            nameGroup.SetBounds(R(16), R(130), R(328), R(22));
            Controls.Add(nameGroup);

            Label linesLabel = MakeLabel("Lines:");
            linesLabel.SetBounds(R(16), R(160), R(66), R(22));
            Controls.Add(linesLabel);

            ConfigureNumeric(_nameLines);
            _nameLines.Minimum = 1;
            _nameLines.Maximum = 4;
            _nameLines.Value = Settings.ClampName(nameLines, true);
            _nameLines.SetBounds(R(86), R(157), R(64), R(26));
            _nameLines.ValueChanged += delegate { UpdateInfo(); };
            Controls.Add(_nameLines);

            Label charsLabel = MakeLabel("Max chars:");
            charsLabel.SetBounds(R(178), R(160), R(72), R(22));
            Controls.Add(charsLabel);

            ConfigureNumeric(_nameChars);
            _nameChars.Minimum = 4;
            _nameChars.Maximum = 40;
            _nameChars.Value = Settings.ClampName(nameChars, false);
            _nameChars.SetBounds(R(254), R(157), R(64), R(26));
            _nameChars.ValueChanged += delegate { UpdateInfo(); };
            Controls.Add(_nameChars);

            Label nameHint = new Label();
            nameHint.Text =
                "Chars spread evenly over the lines (8 -> 4+4; 13 -> 5+4+4).";
            nameHint.ForeColor = Color.FromArgb(170, 200, 235);
            nameHint.Font = new Font("Segoe UI", 9F);
            nameHint.SetBounds(R(16), R(188), R(328), R(20));
            Controls.Add(nameHint);

            Label spacingLabel = MakeLabel("Cell spacing (px):");
            spacingLabel.SetBounds(R(16), R(218), R(110), R(22));
            Controls.Add(spacingLabel);

            ConfigureNumeric(_spacing);
            _spacing.Minimum = 0;
            _spacing.Maximum = 40;
            _spacing.Value = Settings.ClampSpacing(spacing);
            _spacing.SetBounds(R(128), R(215), R(64), R(26));
            Controls.Add(_spacing);

            Label spacingHint = new Label();
            spacingHint.Text = "Gap inside/around every cell.";
            spacingHint.ForeColor = Color.FromArgb(170, 200, 235);
            spacingHint.Font = new Font("Segoe UI", 9F);
            spacingHint.SetBounds(R(200), R(218), R(150), R(20));
            Controls.Add(spacingHint);

            ConfigureCheck(_showTree, "Show D: drive index panel", treeVisible);
            _showTree.SetBounds(R(16), R(254), R(328), R(26));
            Controls.Add(_showTree);

            ConfigureCheck(_showRecent, "Show recent files panel", recentVisible);
            _showRecent.SetBounds(R(16), R(286), R(328), R(26));
            Controls.Add(_showRecent);

            Label pinGroup = new Label();
            pinGroup.Text = "Pinned grid slots (remove to release the slot):";
            pinGroup.ForeColor = Color.White;
            pinGroup.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            pinGroup.SetBounds(R(16), R(318), R(328), R(22));
            Controls.Add(pinGroup);

            _pinHost.BackColor = Color.FromArgb(29, 34, 41);
            _pinHost.BorderStyle = BorderStyle.FixedSingle;
            _pinHost.AutoScroll = true;
            _pinHost.SetBounds(R(16), R(342), R(328), R(96));
            Controls.Add(_pinHost);
            RebuildPins();

            Button save = new Button();
            save.Text = "Save";
            ConfigureButton(save);
            save.BackColor = Color.FromArgb(47, 115, 200);
            save.SetBounds(R(16), R(452), R(150), R(38));
            save.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(save);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            ConfigureButton(cancel);
            cancel.BackColor = Color.FromArgb(58, 66, 76);
            cancel.SetBounds(R(194), R(452), R(150), R(38));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = save;
            CancelButton = cancel;
        }

        // One row per pinned slot: "Slot N: file name" (full path in the
        // tooltip) plus a Remove button that stages the slot for release
        // on Save. Rows are authored at 96 DPI and scaled like the rest.
        private void RebuildPins()
        {
            _pinHost.Controls.Clear();

            int y = R(4);
            foreach (KeyValuePair<int, string> kv in _pinsLive)
            {
                Label l = new Label();
                l.AutoSize = false;
                l.Text = "Slot " + (kv.Key + 1) + ":  " +
                         Path.GetFileName(kv.Value);
                l.ForeColor = Color.White;
                l.Font = new Font("Segoe UI", 9F);
                l.TextAlign = ContentAlignment.MiddleLeft;
                l.AutoEllipsis = true;
                l.SetBounds(R(6), y, R(224), R(24));
                _pinTips.SetToolTip(l, kv.Value);
                _pinHost.Controls.Add(l);

                Button b = new Button();
                b.Text = "Remove";
                ConfigureButton(b);
                b.BackColor = Color.FromArgb(58, 66, 76);
                b.SetBounds(R(236), y, R(66), R(24));
                int slot = kv.Key;
                b.Click += delegate
                {
                    _removedPins.Add(slot);
                    for (int i = 0; i < _pinsLive.Count; i++)
                    {
                        if (_pinsLive[i].Key == slot)
                        {
                            _pinsLive.RemoveAt(i);
                            break;
                        }
                    }
                    RebuildPins();
                };
                _pinHost.Controls.Add(b);

                y += R(28);
            }

            if (_pinsLive.Count == 0)
            {
                Label empty = new Label();
                empty.AutoSize = false;
                empty.Text = "No pinned slots. Right-click a grid cell to pin a file.";
                empty.ForeColor = Color.FromArgb(150, 165, 180);
                empty.Font = new Font("Segoe UI", 9F);
                empty.TextAlign = ContentAlignment.MiddleLeft;
                empty.SetBounds(R(8), R(6), R(300), R(22));
                _pinHost.Controls.Add(empty);
            }
        }

        private Label MakeLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = Color.White;
            l.Font = new Font("Segoe UI", 9.5F);
            return l;
        }

        private void ConfigureNumeric(NumericUpDown n)
        {
            n.BackColor = Color.FromArgb(29, 34, 41);
            n.ForeColor = Color.White;
            n.Font = new Font("Segoe UI", 9.5F);
            n.BorderStyle = BorderStyle.FixedSingle;
        }

        private static void ConfigureCheck(CheckBox c, string text, bool on)
        {
            c.Text = text;
            c.Checked = on;
            c.ForeColor = Color.White;
            c.BackColor = Color.FromArgb(38, 44, 52);
            c.FlatStyle = FlatStyle.Flat;
            c.Font = new Font("Segoe UI", 9.5F);
            c.Cursor = Cursors.Hand;
        }

        private static void ConfigureButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 9.5F);
            b.Cursor = Cursors.Hand;
        }

        private void UpdateInfo()
        {
            int c = (int)_cols.Value;
            int r = (int)_rows.Value;
            int l = (int)_nameLines.Value;
            int ch = (int)_nameChars.Value;
            _info.Text = "= " + (c * r) + " files shown (" +
                         r + " rows x " + c + " cols); up to " +
                         (l * ch) + " chars per name";
        }
    }
}
