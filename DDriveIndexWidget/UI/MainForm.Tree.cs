// Tree behavior: press-vs-drag handling, owner drawing (transparent rows,
// white shadowed text, blue live highlight), lazy loading and root rebuild.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal partial class MainForm
    {
        // Every left press inside the tree is swallowed: it may become a
        // panel drag (mouse moved), or a normal click resolved on release.
        private bool TreePress(Point p)
        {
            _pressActive = true;
            _pressPoint = p;
            return true;
        }

        private void TreeRelease(Point p)
        {
            bool wasDrag = _drag == DragMode.Move;
            if (wasDrag)
            {
                _drag = DragMode.None;
                _settings.Rect = _panel.Bounds;
                _settings.Save();
            }
            _pressActive = false;
            if (wasDrag) return;

            // A real click: resolve what was under the cursor.
            TreeNode node = _tree.GetNodeAt(p);
            if (node == null) return;
            uint flags = HitFlags(p);
            bool onGlyph = (flags & Win32.TvhtOnItemButton) != 0;
            bool onLabel = (flags & Win32.TvhtOnItemLabel) != 0 ||
                           (flags & Win32.TvhtOnItemIcon) != 0;

            if (onGlyph)
            {
                node.Toggle();
            }
            else if (onLabel)
            {
                NodeItem ni = node.Tag as NodeItem;
                if (ni != null && !string.IsNullOrEmpty(ni.Path)) OpenItem(ni.Path);
            }
        }

        private void TreeMouseDown(object sender, MouseEventArgs e)
        {
            // Left presses are swallowed by the tree hook and do not raise
            // this event; only right clicks reach here for the menu.
            if (e.Button == MouseButtons.Right)
            {
                _menuNode = _tree.GetNodeAt(e.Location);
            }
        }

        // The widget never uses a caret: left clicks open items and rows
        // stay fully transparent. When the right-click menu opens, its
        // focus churn makes the native TreeView auto-select its first
        // visible node (the blue bar wrongly jumps to the top row). Drop
        // every caret the native control creates on its own; deferring the
        // reset avoids re-entering the TVN_SELCHANGED notification.
        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && IsHandleCreated && !_tree.IsDisposed)
                        _tree.SelectedNode = null;
                });
            }
            catch { }
        }

        private void TreeMouseMove(object sender, MouseEventArgs e)
        {
            if (!_pressActive) return;
            int dx = e.X - _pressPoint.X;
            int dy = e.Y - _pressPoint.Y;
            if (_drag != DragMode.Move && (dx * dx + dy * dy) >= 36)
            {
                // Movement threshold crossed: start moving the panel.
                BeginDrag(DragMode.Move, _tree.PointToScreen(_pressPoint));
            }
            if (_drag == DragMode.Move)
            {
                ApplyDragDelta(_tree.PointToScreen(e.Location));
            }
        }

        private uint HitFlags(Point p)
        {
            Win32.TvHitTest ht = new Win32.TvHitTest();
            ht.Pt.X = p.X;
            ht.Pt.Y = p.Y;
            Win32.SendMessage(_tree.Handle, Win32.TvmHitTest, IntPtr.Zero, ref ht);
            return ht.Flags;
        }

        // Rows stay fully transparent; the only row background ever drawn is
        // the blue live-update highlight (no selection bar — clicks open the
        // item immediately, so selection state adds nothing but a dark strip).
        // Folders are bold (via NodeFont); white text gets a black shadow.
        private void TreeDrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            Rectangle row = e.Bounds;
            int width = _tree.ClientSize.Width - row.X - 2;
            if (width < 0) width = 0;
            Rectangle bar = new Rectangle(row.X, row.Y, width, row.Height);

            bool hot = _highlightedNodes.Contains(e.Node);
            if (hot)
                e.Graphics.FillRectangle(WidgetColors.HighlightBrush, bar);

            Font font = e.Node.NodeFont ?? e.Node.TreeView.Font;
            // No EndEllipsis: labels are length-capped by TruncateLabel
            // (12 CJK chars / 24 half-width units). Text is drawn into the
            // full-width bar instead of the (sometimes very narrow) native
            // label rect, so short names like two CJK chars never collapse
            // into a bogus one-char ellipsis.
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            Rectangle shadow = bar;
            shadow.Offset(1, 1);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, font, shadow,
                Color.FromArgb(190, 0, 0, 0), Color.Transparent, flags);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, font, bar,
                Color.White, Color.Transparent, flags);
        }

        private void TreeBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (IsUnloaded(e.Node)) LoadChildren(e.Node);
        }

        private static bool IsUnloaded(TreeNode node)
        {
            return node.Nodes.Count == 1 &&
                   string.Equals(node.Nodes[0].Tag as string, Sentinel, StringComparison.Ordinal);
        }

        private void OpenItem(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        // ---- tree content ----
        // There is intentionally no "D:" root node: the tree's top level is
        // the visible level-2 folders plus pinned folders, so the first row
        // never shows a full-width drive-letter bar.

        private TreeNode MakeNode(NodeItem item)
        {
            string label = FolderScanner.LeafName(item.Path);
            TreeNode n = new TreeNode(TruncateLabel(label));
            n.Tag = item;
            n.ToolTipText = label; // full name on hover for truncated rows
            if (item.IsFolder)
            {
                n.NodeFont = _folderFont;
                n.Nodes.Add(new TreeNode { Tag = Sentinel });
            }
            return n;
        }

        // Label budget: at most 12 CJK characters wide. A CJK/full-width
        // glyph counts as 2 units, everything else as 1, so the same budget
        // fits 24 plain ASCII chars. Names within the budget are shown whole
        // ("jian li" stays "jian li"); longer names keep the first 12-CJK-
        // widths worth of characters followed by "...".
        private const int MaxLabelUnits = 24;

        private static string TruncateLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int units = 0;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool pair = char.IsHighSurrogate(c) && i + 1 < name.Length &&
                            char.IsLowSurrogate(name[i + 1]);
                int w = pair ? 2 : (IsWideGlyph(c) ? 2 : 1);
                if (units + w > MaxLabelUnits)
                    return name.Substring(0, i) + "...";
                units += w;
                if (pair) i++;
            }
            return name;
        }

        private static bool IsWideGlyph(char c)
        {
            // Ranges that render at the full-width CJK cell: CJK radicals
            // and symbols, kana, CJK ideographs (incl. ext-A), Hangul, Yi,
            // compatibility ideographs and full-width punctuation/forms.
            return (c >= 0x1100 && c <= 0x115F) ||
                   (c >= 0x2E80 && c <= 0x303E) ||
                   (c >= 0x3041 && c <= 0x33FF) ||
                   (c >= 0x3400 && c <= 0x4DBF) ||
                   (c >= 0x4E00 && c <= 0x9FFF) ||
                   (c >= 0xA000 && c <= 0xA4CF) ||
                   (c >= 0xAC00 && c <= 0xD7A3) ||
                   (c >= 0xF900 && c <= 0xFAFF) ||
                   (c >= 0xFE30 && c <= 0xFE4F) ||
                   (c >= 0xFF00 && c <= 0xFF60) ||
                   (c >= 0xFFE0 && c <= 0xFFE6);
        }

        private void LoadChildren(TreeNode node)
        {
            NodeItem folder = node.Tag as NodeItem;
            string path = folder == null ? null : folder.Path;
            if (string.IsNullOrEmpty(path)) return;

            List<NodeItem> items =
                FolderScanner.EnumerateChildren(path, _settings.Hidden);

            node.Nodes.Clear();
            foreach (NodeItem item in items)
                node.Nodes.Add(MakeNode(item));
        }

        // Rebuild one already-expanded branch, preserving expansion state.
        private void RebuildLoaded(TreeNode node)
        {
            if (node == null) return;
            NodeItem folder = node.Tag as NodeItem;
            if (folder == null || string.IsNullOrEmpty(folder.Path)) return;

            HashSet<string> expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TreeNode child in node.Nodes)
            {
                NodeItem ni = child.Tag as NodeItem;
                if (ni != null && child.IsExpanded && !IsUnloaded(child))
                    expandedPaths.Add(ni.Path);
            }

            LoadChildren(node);
            foreach (TreeNode child in node.Nodes)
            {
                NodeItem ni = child.Tag as NodeItem;
                if (ni != null && expandedPaths.Contains(ni.Path))
                {
                    RebuildLoaded(child);
                    child.Expand();
                }
            }
        }

        private void RebuildRoot()
        {
            EnsureTreeBuilt();

            // Remember which top-level folders were expanded (their deeper
            // expansion is preserved recursively by RebuildLoaded).
            HashSet<string> expandedTop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TreeNode n in _tree.Nodes)
            {
                NodeItem ni = n.Tag as NodeItem;
                if (ni != null && n.IsExpanded && !IsUnloaded(n))
                    expandedTop.Add(ni.Path);
            }

            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            foreach (NodeItem item in
                FolderScanner.GetRootEntries(_settings.Hidden, _settings.Added))
            {
                _tree.Nodes.Add(MakeNode(item));
            }
            foreach (TreeNode n in _tree.Nodes)
            {
                NodeItem ni = n.Tag as NodeItem;
                if (ni != null && expandedTop.Contains(ni.Path))
                {
                    RebuildLoaded(n);
                    n.Expand();
                }
            }
            _tree.EndUpdate();
        }

        private void EnsureTreeBuilt()
        {
            if (_treeLoaded) return;
            _treeLoaded = true;

            foreach (NodeItem item in
                FolderScanner.GetRootEntries(_settings.Hidden, _settings.Added))
            {
                _tree.Nodes.Add(MakeNode(item));
            }
        }

        // ---- wheel: plain scrolls the tree, Ctrl zooms the font ----

        // The tree's own scrollbars are hidden (TVS_NOSCROLL), so plain
        // wheel ticks are forwarded as WM_VSCROLL line/page commands.
        private void ScrollTree(int notches)
        {
            if (notches == 0) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { ScrollTree(notches); }); }
                catch { }
                return;
            }

            int lines = 3;
            Win32.SystemParametersInfo(Win32.SpiGetWheelScrollLines, 0, ref lines, 0);

            int cmdUp, cmdDown;
            int steps;
            if (lines <= 0)
            {
                // "One screen per notch" setting.
                cmdUp = Win32.SbPageUp;
                cmdDown = Win32.SbPageDown;
                steps = 1;
            }
            else
            {
                cmdUp = Win32.SbLineUp;
                cmdDown = Win32.SbLineDown;
                steps = lines;
            }

            int cmd = notches > 0 ? cmdUp : cmdDown;
            int count = steps * Math.Abs(notches);
            for (int i = 0; i < count; i++)
                Win32.SendMessage(_tree.Handle, Win32.WmVScroll, (IntPtr)cmd, IntPtr.Zero);
        }

        private const float MinFontSize = 7f;
        private const float MaxFontSize = 28f;
        private const float ZoomStep = 0.75f;

        private void ZoomTree(int notches)
        {
            if (notches == 0) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { ZoomTree(notches); }); }
                catch { }
                return;
            }
            float size = _tree.Font.Size + notches * ZoomStep;
            if (size < MinFontSize) size = MinFontSize;
            if (size > MaxFontSize) size = MaxFontSize;
            if (Math.Abs(size - _tree.Font.Size) < 0.01f) return;
            SetTreeFont(size);
            _settings.FontSize = size;
            _settings.Save();
        }

        private void SetTreeFont(float size)
        {
            Font oldBase = _tree.Font;
            Font oldFolder = _folderFont;

            Font newBase = new Font("Segoe UI", size);
            _folderFont = new Font(newBase, FontStyle.Bold);
            _tree.Font = newBase;
            _tree.ItemHeight = newBase.Height + 8;

            ApplyFolderFonts(_tree.Nodes);
            _tree.Refresh();

            if (oldFolder != null) oldFolder.Dispose();
            oldBase.Dispose();
        }

        private void ApplyFolderFonts(TreeNodeCollection nodes)
        {
            foreach (TreeNode n in nodes)
            {
                NodeItem ni = n.Tag as NodeItem;
                if (ni != null)
                    n.NodeFont = ni.IsFolder ? _folderFont : null;
                if (n.Nodes.Count > 0) ApplyFolderFonts(n.Nodes);
            }
        }
    }
}
