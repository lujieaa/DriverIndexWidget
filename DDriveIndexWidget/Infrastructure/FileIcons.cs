// Real default-application icons for file types, fetched once per
// extension from the shell (SHGetFileInfo + SHGFI_USEFILEATTRIBUTES)
// and cached for the process lifetime as a high-resolution bitmap.
//
// A synthetic "name.ext" with USEFILEATTRIBUTES returns the registered
// type icon without needing the file itself, so every cell of the same
// extension shares one image. The LARGE icon is kept (32px logical /
// 64px at 200% DPI) and drawn scaled down - downscaling stays crisp.
//
// Drawing note: DrawIconEx(DI_NORMAL) was observed to fail silently on
// this machine (returns FALSE, paints nothing); GDI+ DrawImage of
// Icon.ToBitmap() works, hence the bitmap cache.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace DDriveIndexWallpaper
{
    internal static class FileIcons
    {
        private static readonly Dictionary<string, Bitmap> Cache =
            new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly object LockObj = new object();

        public static Bitmap Get(string path)
        {
            string ext;
            try { ext = Path.GetExtension(path ?? ""); }
            catch { ext = ""; }
            if (ext == null) ext = "";
            string key = ext.ToLowerInvariant();

            lock (LockObj)
            {
                Bitmap bmp;
                if (Cache.TryGetValue(key, out bmp)) return bmp;

                bmp = Load(key);
                Cache[key] = bmp;
                return bmp;
            }
        }

        private static Bitmap Load(string ext)
        {
            try
            {
                Win32.ShFileInfo fi = new Win32.ShFileInfo();
                uint cb = (uint)Marshal.SizeOf(typeof(Win32.ShFileInfo));
                string probe = string.IsNullOrEmpty(ext) ? "noextfile" : "file" + ext;
                IntPtr res = Win32.SHGetFileInfo(probe,
                    Win32.FileAttributeNormal, ref fi, cb,
                    Win32.ShgfiIcon | Win32.ShgfiLargeIcon |
                    Win32.ShgfiUseFileAttributes);
                if (res != IntPtr.Zero && fi.HIcon != IntPtr.Zero)
                {
                    using (Icon icon = Icon.FromHandle(fi.HIcon))
                    using (Bitmap raw = icon.ToBitmap())
                    {
                        // Normalize into a 32bpp ARGB square bitmap at the
                        // icon's own large size so cells just DrawImage it.
                        Bitmap bmp = new Bitmap(raw.Width, raw.Height,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
                        }
                        Win32.DestroyIcon(fi.HIcon);
                        return bmp;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("FileIcons load failed for '" + ext + "': " + ex.Message);
            }
            return null;
        }

        public static void DisposeAll()
        {
            lock (LockObj)
            {
                foreach (KeyValuePair<string, Bitmap> kv in Cache)
                {
                    try { if (kv.Value != null) kv.Value.Dispose(); }
                    catch { }
                }
                Cache.Clear();
            }
        }
    }
}
