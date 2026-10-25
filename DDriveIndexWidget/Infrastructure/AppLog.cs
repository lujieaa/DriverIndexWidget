// Tiny file logger: %TEMP%\D-DriveIndexWallpaper.log
using System;
using System.IO;

namespace DDriveIndexWallpaper
{
    internal static class AppLog
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "D-DriveIndexWallpaper.log");

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("HH:mm:ss.fff  ") + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
