// MainForm shell: borderless full-screen transparent host window that
// contains the floating panel. Construction, window styles/behavior and
// the control tree live here; geometry/tree/menu/watcher are split into
// the other MainForm.*.cs partial files.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDriveIndexWallpaper
{
    internal partial class MainForm : Form
    {
        // Tree lazy-load placeholder tag.
        private const string Sentinel = "__unloaded__";

        private readonly Settings _settings = Settings.Load();

        private readonly Panel _panel = new Panel();
        private readonly IndexTreeView _tree = new IndexTreeView();
        private readonly GlyphButton _btnAdd = new GlyphButton("Add");
        private readonly GlyphButton _btnMenu = new GlyphButton("\u2630");
        private readonly Button _grip = new Button();
        // 7 hot zones: 4 edges + 3 corners (bottom-right is the grip button).
        private readonly Panel[] _zones = new Panel[7];
        private DragMode _hoverZone = DragMode.None;
        private readonly Timer _hoverTimer = new Timer();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();

        // The FileSystemWatcher field itself is declared in MainForm.Watcher.cs.
        private readonly Timer _debounce = new Timer();
        private readonly Timer _bottomKeeper = new Timer();
        private readonly Timer _unhighlight = new Timer();
        private System.Collections.Generic.List<TreeNode> _highlightedNodes =
            new System.Collections.Generic.List<TreeNode>();
        private bool _treeLoaded;
        private TreeNode _menuNode;

        // Drag / press state shared by layout and tree partials.
        private DragMode _drag = DragMode.None;
        private Point _dragStart;           // screen coords at mouse down
        private Rectangle _dragStartRect;   // panel bounds at mouse down
        private bool _pressActive;          // a swallowed tree press is live
        private Point _pressPoint;          // tree-client coords of the press

        // One shared bold font for every folder node (recreated on zoom).
        private Font _folderFont;
        private MouseWheelHook _wheelHook;

        public MainForm()
        {
            Text = "D: Drive Index";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = WidgetColors.Key;
            TransparencyKey = WidgetColors.Key;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = false;

            BuildUi();

            _debounce.Interval = 700;
            _debounce.Tick += DebounceTick;

            _bottomKeeper.Interval = 1000;
            _bottomKeeper.Tick += delegate { KeepAtBottom(); };

            _unhighlight.Interval = 15000;
            _unhighlight.Tick += delegate
            {
                _unhighlight.Stop();
                _highlightedNodes.Clear();
                _tree.Invalidate();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Win32.WsExToolWindow | Win32.WsExNoActivate;
                return cp;
            }
        }

        private void BuildUi()
        {
            _panel.BackColor = WidgetColors.Key; // see-through everywhere
            Controls.Add(_panel);

            _tree.BackColor = WidgetColors.Key;
            _tree.BorderStyle = BorderStyle.None;
            _tree.ForeColor = Color.White;
            _tree.Font = new Font("Segoe UI", _settings.FontSize);
            _folderFont = new Font(_tree.Font, FontStyle.Bold);
            _tree.ShowLines = false;
            _tree.ShowRootLines = false;
            _tree.HideSelection = false;
            _tree.ShowNodeToolTips = true;
            _tree.ItemHeight = _tree.Font.Height + 8;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.DrawNode += TreeDrawNode;
            _tree.BeforeExpand += TreeBeforeExpand;
            _tree.MouseDown += TreeMouseDown;
            _tree.MouseMove += TreeMouseMove;
            _tree.AfterSelect += TreeAfterSelect;
            _tree.ContextMenuStrip = _menu;
            _tree.PressHook = TreePress;
            _tree.ReleaseHook = TreeRelease;
            _panel.Controls.Add(_tree);

            // Transparent glyph buttons (no opaque block on the wallpaper).
            _btnAdd.Click += delegate { ShowAddDialog(); };
            _btnMenu.Click += delegate
            {
                _menuNode = null;
                _menu.Show(_btnMenu, new Point(0, _btnMenu.Height));
            };
            // Form-level children so the buttons always render above the tree.
            Controls.Add(_btnMenu);
            Controls.Add(_btnAdd);
            _btnAdd.BringToFront();
            _btnMenu.BringToFront();

            ToolTip tips = new ToolTip();
            tips.SetToolTip(_btnAdd, "Add a folder to the panel");
            tips.SetToolTip(_btnMenu, "Menu (rescan / exit)");

            // Hidden-until-hover resize zones: 4 edges + 3 corners. They are
            // painted with the transparency key (inert, see-through) until
            // the cursor approaches, when HoverTick lights the zone up.
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
            for (int i = 0; i < _zones.Length; i++)
            {
                Panel z = new Panel();
                z.BackColor = WidgetColors.Key;
                z.Cursor = zoneCursors[i];
                z.Tag = zoneModes[i];
                AttachDrag(z, zoneModes[i]);
                _zones[i] = z;
                _panel.Controls.Add(z);
            }

            // Permanent visible bottom-right grip: a plain deep-blue square.
            _grip.Text = "";
            _grip.Size = new Size(GripSize, GripSize);
            _grip.FlatStyle = FlatStyle.Flat;
            _grip.FlatAppearance.BorderSize = 0;
            _grip.FlatAppearance.BorderColor = WidgetColors.ResizeGrip;
            _grip.FlatAppearance.MouseOverBackColor = WidgetColors.ResizeGripHover;
            _grip.FlatAppearance.MouseDownBackColor = WidgetColors.ResizeGripPressed;
            _grip.BackColor = WidgetColors.ResizeGrip;
            _grip.UseVisualStyleBackColor = false;
            _grip.Cursor = Cursors.SizeNWSE;
            AttachDrag(_grip, DragMode.BottomRight);
            Controls.Add(_grip);
            _grip.BringToFront();
            tips.SetToolTip(_grip, "Drag to resize");

            // Edge/corner reveal on proximity.
            _hoverTimer.Interval = 50;
            _hoverTimer.Tick += delegate { HoverTick(); };
            _hoverTimer.Start();

            _menu.Opening += MenuOpening;

            ApplyInitialRect();
        }

        // ---- window behavior ----

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        // Outside the panel rectangle the form is fully click-through.
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == Win32.WmNcHitTest)
            {
                Rectangle r = _panel.RectangleToScreen(_panel.ClientRectangle);
                if (!r.Contains(Cursor.Position)) m.Result = (IntPtr)Win32.HtTransparent;
            }
        }

        private void KeepAtBottom()
        {
            if (IsHandleCreated && Win32.IsIconic(Handle))
                Win32.ShowWindow(Handle, Win32.SwRestore);
            Win32.SetWindowPos(Handle, Win32.HwndBottom, 0, 0, 0, 0,
                Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            KeepAtBottom();
            StartWatcher();
            EnsureTreeBuilt();
            _bottomKeeper.Start();

            // Wheel actions work on hover because the form never gets
            // keyboard focus; a low-level mouse hook catches the wheel:
            // plain wheel scrolls, Ctrl+wheel zooms the font.
            _wheelHook = new MouseWheelHook(
                delegate { return _panel.RectangleToScreen(_panel.ClientRectangle); },
                ZoomTree, ScrollTree);
            _wheelHook.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_wheelHook != null) _wheelHook.Dispose();
            if (_folderFont != null) _folderFont.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_panel != null && IsHandleCreated)
            {
                // Keep the panel inside the screen if the screen itself changed.
                Rectangle b = Bounds;
                Rectangle r = _panel.Bounds;
                if (r.Right > b.Right - 40 || r.Bottom > b.Bottom - 20)
                {
                    r.Width = Math.Min(r.Width, MaxW());
                    r.Height = Math.Min(r.Height, MaxH());
                    r.X = Math.Min(r.X, b.Right - r.Width - 8);
                    r.Y = Math.Min(r.Y, b.Bottom - r.Height - 8);
                    _panel.Bounds = r;
                }
                LayoutChildren();
            }
        }
    }
}
