// Single system tray icon shared by both panels: quick toggles for
// panel visibility, the settings dialog, rescan and exit. The icon is
// generated at runtime (blue square, white D glyph) so no resource
// file is needed.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal partial class MainForm
    {
        private void BuildTray()
        {
            try
            {
                _tray.Icon = MakeTrayIcon();
                _tray.Text = "Desktop widget: D: index + recent files";
                _tray.Visible = true;
                _tray.DoubleClick += delegate { ShowSettings(); };
                _tray.ContextMenuStrip = _trayMenu;
            }
            catch (Exception ex)
            {
                AppLog.Write("tray init failed: " + ex.Message);
            }
        }

        private static Icon MakeTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush blue = new SolidBrush(WidgetColors.Highlight))
                    g.FillRectangle(blue, 0, 0, 16, 16);
                using (Font f = new Font("Segoe UI", 9F, FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, "D", f,
                        new Rectangle(-1, -1, 16, 16), Color.White,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void TrayMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _trayMenu.Items.Clear();

            ToolStripMenuItem tree =
                new ToolStripMenuItem("Show D: drive index panel");
            tree.Checked = _settings.TreeVisible;
            tree.Click += delegate
            {
                _settings.TreeVisible = !_settings.TreeVisible;
                _settings.Save();
                ApplyPanelVisibility();
            };
            _trayMenu.Items.Add(tree);

            ToolStripMenuItem recent =
                new ToolStripMenuItem("Show recent files panel");
            recent.Checked = _settings.RecentVisible;
            recent.Click += delegate
            {
                _settings.RecentVisible = !_settings.RecentVisible;
                _settings.Save();
                ApplyPanelVisibility();
            };
            _trayMenu.Items.Add(recent);

            _trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { ShowSettings(); };
            _trayMenu.Items.Add(settings);

            ToolStripMenuItem rescan = new ToolStripMenuItem("Rescan D: now");
            rescan.Click += delegate { RequestRescan(); };
            _trayMenu.Items.Add(rescan);

            _trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { Application.Exit(); };
            _trayMenu.Items.Add(exit);
        }
    }
}
