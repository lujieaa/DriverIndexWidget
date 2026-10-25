// Entry point: single-instance widget run, or -install / -uninstall to
// manage the Startup folder shortcut.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    static class Program
    {
        private const string MutexName = "Local\\DDriveIndexWallpaper-SingleInstance";

        [STAThread]
        static void Main(string[] args)
        {
            string arg = (args != null && args.Length > 0) ? args[0].ToLowerInvariant() : "";
            if (arg == "-install" || arg == "/install")
            {
                StartupShortcut(false);
                return;
            }
            if (arg == "-uninstall" || arg == "/uninstall")
            {
                StartupShortcut(true);
                return;
            }

            bool created;
            using (System.Threading.Mutex mutex =
                   new System.Threading.Mutex(true, MutexName, out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                GC.KeepAlive(mutex);
            }
        }

        // Creates or removes the Startup folder shortcut via WScript.Shell COM.
        private static void StartupShortcut(bool remove)
        {
            string lnkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "D-DriveIndexWallpaper.lnk");

            if (remove)
            {
                try { if (File.Exists(lnkPath)) File.Delete(lnkPath); }
                catch { }
                return;
            }

            string exe = Application.ExecutablePath;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                try
                {
                    dynamic sc = shell.CreateShortcut(lnkPath);
                    sc.TargetPath = exe;
                    sc.Arguments = "";
                    sc.WorkingDirectory = Path.GetDirectoryName(exe);
                    sc.Description = "D: Drive Live Index Wallpaper";
                    sc.WindowStyle = 7;
                    sc.Save();
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(Path.GetTempPath(), "D-DriveIndexWallpaper-install.log"),
                        DateTime.Now.ToString("HH:mm:ss") + "  " + ex + Environment.NewLine);
                }
                catch { }
                throw;
            }
        }
    }
}
