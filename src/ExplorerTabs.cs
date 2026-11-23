// ExplorerTabs — タブが2段・3段と折り返すフォルダ窓
// 中身は Windows 標準のフォルダ表示（ExplorerBrowser）を埋め込む。
// 組み立て: build.bat（Windows に入っている .NET Framework 4 の csc.exe）
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ExplorerTabs
{
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; } }

    [StructLayout(LayoutKind.Sequential)]
    struct FOLDERSETTINGS { public uint ViewMode; public uint fFlags; }

    [ComImport, Guid("dfd3b6b5-c10c-4be9-85f6-a66969f402f6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IExplorerBrowser
    {
        void Initialize(IntPtr hwndParent, [In] ref RECT prc, [In] ref FOLDERSETTINGS pfs);
        void Destroy();
        void SetRect(IntPtr phdwp, RECT rcBrowser);
        void SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string pszPropertyBag);
        void SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string pszEmptyText);
        void SetFolderSettings([In] ref FOLDERSETTINGS pfs);
        void Advise(IExplorerBrowserEvents psbe, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint dwFlag);
        void GetOptions(out uint pdwFlag);
        [PreserveSig] int BrowseToIDList(IntPtr pidl, uint uFlags);
        void BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object punk, uint uFlags);
        void FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, int dwFlags);
        void RemoveAll();
        void GetCurrentView(ref Guid riid, out IntPtr ppv);
    }

    [ComImport, Guid("361bbdc7-e6ee-4e13-be58-58e2240c810f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IExplorerBrowserEvents
    {
        [PreserveSig] int OnNavigationPending(IntPtr pidlFolder);
        [PreserveSig] int OnViewCreated([MarshalAs(UnmanagedType.IUnknown)] object psv);
        [PreserveSig] int OnNavigationComplete(IntPtr pidlFolder);
        [PreserveSig] int OnNavigationFailed(IntPtr pidlFolder);
    }

    [ComImport, Guid("71f96385-ddd6-48d3-a0c1-ae06e8b055fb")]
    class ExplorerBrowserClass { }

    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Sequential)]
    struct SORTCOLUMN { public PROPERTYKEY propkey; public int direction; }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam; public IntPtr lParam; public int time; public int x, y; }

    // ExplorerBrowser にキー（Ctrl+C/V/X/A/Z・Del・F2 など）を渡す口。WinForms の窓ではこれを呼ばないと中のフォルダ表示にショートカットが届かない
    [ComImport, Guid("68284faa-6a48-11d0-8c78-00c04fd918b4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IInputObject
    {
        [PreserveSig] int UIActivateIO([MarshalAs(UnmanagedType.Bool)] bool fActivate, ref MSG msg);
        [PreserveSig] int HasFocusIO();
        [PreserveSig] int TranslateAcceleratorIO(ref MSG msg);
    }

    // IFolderView（14 個）＋ IFolderView2。使わない物は並びを合わせるための空の宣言（並びは SDK の ShObjIdl_core.h で確認済み）
    [ComImport, Guid("1af3a467-214f-4298-908e-06b03e0b39f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFolderView2
    {
        void _GetCurrentViewMode(); void _SetCurrentViewMode(); void _GetFolder(); void _Item();
        [PreserveSig] int ItemCount(uint flags, out int count);
        void _Items(); void _GetSelectionMarkedItem(); void _GetFocusedItem(); void _GetItemPosition(); void _GetSpacing();
        void _GetDefaultSpacing(); void _GetAutoArrange();
        [PreserveSig] int SelectItem(int iItem, uint flags);
        void _SelectAndPositionItems();
        [PreserveSig] int SetGroupBy([In] ref PROPERTYKEY key, [MarshalAs(UnmanagedType.Bool)] bool fAscending);
        void _GetGroupBy(); void _SetViewProperty(); void _GetViewProperty(); void _SetTileViewProperties();
        void _SetExtendedTileViewProperties(); void _SetText(); void _SetCurrentFolderFlags(); void _GetCurrentFolderFlags();
        [PreserveSig] int GetSortColumnCount(out int count);
        [PreserveSig] int SetSortColumns([In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] SORTCOLUMN[] cols, int count);
        [PreserveSig] int GetSortColumns([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] SORTCOLUMN[] cols, int count);
        void _GetItem(); void _GetVisibleItem(); void _GetSelectedItem(); void _GetSelection(); void _GetSelectionState(); void _InvokeVerbOnSelection();
        [PreserveSig] int SetViewModeAndIconSize(int mode, int iconSize);
        [PreserveSig] int GetViewModeAndIconSize(out int mode, out int iconSize);
    }

    // フォルダごとの「表示（詳細・アイコン等と大きさ）」と「並び替え」を覚える。%APPDATA%\ExplorerTabs\views.txt
    // 1行＝ パス <TAB> 表示 <TAB> 大きさ <TAB> 並び（fmtid,pid,向き;…）
    static class ViewStore
    {
        public static string File = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExplorerTabs", "views.txt");
        static System.Collections.Generic.Dictionary<string, string[]> map;

        static void Load()
        {
            if (map != null) return;
            map = new System.Collections.Generic.Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!System.IO.File.Exists(File)) return;
                foreach (var line in System.IO.File.ReadAllLines(File, Encoding.UTF8))
                {
                    var f = line.Split('\t');
                    if (f.Length >= 4 && f[0] != "") map[f[0]] = f;
                }
            }
            catch { }
        }

        public static void Set(string path, int mode, int size, SORTCOLUMN[] cols)
        {
            if (string.IsNullOrEmpty(path)) return;
            Load();
            var sb = new StringBuilder();
            foreach (var c in cols) { if (sb.Length > 0) sb.Append(';'); sb.Append(c.propkey.fmtid.ToString("D")).Append(',').Append(c.propkey.pid).Append(',').Append(c.direction); }
            map[path] = new[] { path, mode.ToString(), size.ToString(), sb.ToString() };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File));
                var lines = new System.Collections.Generic.List<string>();
                foreach (var v in map.Values) lines.Add(string.Join("\t", v));
                System.IO.File.WriteAllLines(File, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        public static bool TryGet(string path, out int mode, out int size, out SORTCOLUMN[] cols)
        {
            mode = size = 0; cols = null;
            if (string.IsNullOrEmpty(path)) return false;
            Load();
            string[] f;
            if (!map.TryGetValue(path, out f)) return false;
            try
            {
                mode = int.Parse(f[1]); size = int.Parse(f[2]);
                var list = new System.Collections.Generic.List<SORTCOLUMN>();
                foreach (var s in f[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = s.Split(',');
                    list.Add(new SORTCOLUMN { propkey = new PROPERTYKEY { fmtid = new Guid(p[0]), pid = uint.Parse(p[1]) }, direction = int.Parse(p[2]) });
                }
                cols = list.ToArray();
                return true;
            }
            catch { return false; }
        }
    }

    static class Native
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName(string name, IntPtr pbc, out IntPtr pidl, uint sfgaoIn, out uint psfgaoOut);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdnName, out IntPtr ppszName);
        public const uint SBSP_ABSOLUTE = 0x0, SBSP_PARENT = 0x2000, SBSP_NAVIGATEBACK = 0x4000, SBSP_NAVIGATEFORWARD = 0x8000;
        public const uint SIGDN_NORMALDISPLAY = 0, SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
    }

    class BrowserPage : TabPage, IExplorerBrowserEvents
    {
        IExplorerBrowser eb;
        uint cookie;
        public string CurrentPath = "";
        public event EventHandler Navigated;

        public BrowserPage(string path)
        {
            Text = "…";
            UseVisualStyleBackColor = true;
            HandleCreated += (s, e) => Init(path);
            Resize += (s, e) => Fit();
        }

        void Init(string path)
        {
            eb = (IExplorerBrowser)new ExplorerBrowserClass();
            var fs = new FOLDERSETTINGS { ViewMode = 4 /*詳細*/, fFlags = 0 };
            var rc = new RECT(0, 0, ClientSize.Width, ClientSize.Height);
            eb.Initialize(Handle, ref rc, ref fs);
            eb.SetOptions(0x2 | 0x80 /*EBO_SHOWFRAMES：左のツリー｜EBO_NOPERSISTVIEWSTATE：表示の設定をエクスプローラー側に書き残さない*/);
            eb.Advise(this, out cookie);
            Go(path);
        }

        void Fit()
        {
            if (eb != null) eb.SetRect(IntPtr.Zero, new RECT(0, 0, ClientSize.Width, ClientSize.Height));
        }

        public bool Go(string path)
        {
            if (eb == null) return false;
            IntPtr pidl; uint a;
            if (Native.SHParseDisplayName(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')), IntPtr.Zero, out pidl, 0, out a) != 0) return false;
            try { return eb.BrowseToIDList(pidl, Native.SBSP_ABSOLUTE) == 0; }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }

        public void Back() { if (eb != null) eb.BrowseToIDList(IntPtr.Zero, Native.SBSP_NAVIGATEBACK); }
        public void Forward() { if (eb != null) eb.BrowseToIDList(IntPtr.Zero, Native.SBSP_NAVIGATEFORWARD); }
        public void Up() { if (eb != null) eb.BrowseToIDList(IntPtr.Zero, Native.SBSP_PARENT); }

        // キーを中のフォルダ表示（右の一覧・左のツリー）に渡す。使われたら true
        public bool TranslateKey(ref Message m)
        {
            var io = eb as IInputObject;
            if (io == null) return false;
            var msg = new MSG { hwnd = m.HWnd, message = m.Msg, wParam = m.WParam, lParam = m.LParam };
            try { return io.TranslateAcceleratorIO(ref msg) == 0; } catch { return false; }
        }

        public void WithView(Action<IFolderView2> f)
        {
            if (eb == null) return;
            IntPtr pv = IntPtr.Zero;
            try
            {
                var iid = typeof(IFolderView2).GUID;
                eb.GetCurrentView(ref iid, out pv);
                if (pv == IntPtr.Zero) return;
                var fv = (IFolderView2)Marshal.GetObjectForIUnknown(pv);
                try { f(fv); } finally { Marshal.ReleaseComObject(fv); }
            }
            catch { }
            finally { if (pv != IntPtr.Zero) Marshal.Release(pv); }
        }

        // いまのフォルダの表示と並び替えを覚える（別のフォルダへ移る直前・タブを閉じる時）
        public void SaveView()
        {
            if (CurrentPath == "") return;
            string path = CurrentPath;
            WithView(fv =>
            {
                int mode, size, n;
                if (fv.GetViewModeAndIconSize(out mode, out size) != 0) return;
                if (fv.GetSortColumnCount(out n) != 0 || n < 0 || n > 16) return;
                var cols = new SORTCOLUMN[n];
                if (n > 0 && fv.GetSortColumns(cols, n) != 0) return;
                ViewStore.Set(path, mode, size, cols);
            });
        }

        // 覚えてある表示と並び替えを戻す（フォルダを開いた直後）
        void RestoreView()
        {
            int mode, size; SORTCOLUMN[] cols;
            if (!ViewStore.TryGet(CurrentPath, out mode, out size, out cols)) return;
            WithView(fv =>
            {
                if (mode > 0) fv.SetViewModeAndIconSize(mode, size);
                if (cols.Length > 0) fv.SetSortColumns(cols, cols.Length);
            });
        }

        public void Close()
        {
            if (eb == null) return;
            SaveView();
            try { eb.Unadvise(cookie); eb.Destroy(); } catch { }
            Marshal.ReleaseComObject(eb); eb = null;
        }

        static string ShellName(IntPtr pidl, uint kind)
        {
            IntPtr p;
            if (Native.SHGetNameFromIDList(pidl, kind, out p) != 0) return null;
            try { return Marshal.PtrToStringUni(p); } finally { Marshal.FreeCoTaskMem(p); }
        }

        public int OnNavigationPending(IntPtr pidl) { SaveView(); return 0; }
        public int OnViewCreated(object psv) { return 0; }
        public int OnNavigationFailed(IntPtr pidl) { return 0; }
        // グループ化を強制で「なし」にする（ダウンロードの日付別などを毎回外す）
        void NoGrouping()
        {
            WithView(fv =>
            {
                var none = new PROPERTYKEY { fmtid = Guid.Empty, pid = 0 }; // PKEY_Null＝グループ化なし
                fv.SetGroupBy(ref none, true);
            });
        }

        public int OnNavigationComplete(IntPtr pidl)
        {
            var sb = new StringBuilder(1024);
            CurrentPath = Native.SHGetPathFromIDListW(pidl, sb) && sb.Length > 0 ? sb.ToString() : (ShellName(pidl, Native.SIGDN_DESKTOPABSOLUTEPARSING) ?? "");
            string title = ShellName(pidl, Native.SIGDN_NORMALDISPLAY) ?? CurrentPath;
            Text = title.Length > 24 ? title.Substring(0, 23) + "…" : title;
            ToolTipText = CurrentPath;
            NoGrouping();
            RestoreView();
            if (Navigated != null) Navigated(this, EventArgs.Empty);
            return 0;
        }
    }

    class MainForm : Form, IMessageFilter
    {
        TabControl tabs = new TabControl();
        TextBox address = new TextBox();
        string startDir;

        public MainForm(string[] args)
        {
            Text = "ExplorerTabs";
            Size = new Size(1300, 850);
            startDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var bar = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(3) };
            var btnBack = MakeBtn("←", "戻る (Alt+←)", (s, e) => Cur(p => p.Back()));
            var btnFwd = MakeBtn("→", "進む (Alt+→)", (s, e) => Cur(p => p.Forward()));
            var btnUp = MakeBtn("↑", "上へ (Alt+↑)", (s, e) => Cur(p => p.Up()));
            var btnNew = MakeBtn("＋", "新しいタブ (Ctrl+T)", (s, e) => NewTab(CurPath()));
            address.Dock = DockStyle.Fill;
            address.Font = new Font("Yu Gothic UI", 10f);
            address.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                var p = tabs.SelectedTab as BrowserPage;
                if (p == null || !p.Go(address.Text)) System.Media.SystemSounds.Beep.Play();
            };
            btnNew.Dock = btnUp.Dock = btnFwd.Dock = btnBack.Dock = DockStyle.Left;
            bar.Controls.Add(address);
            bar.Controls.Add(btnNew); bar.Controls.Add(btnUp); bar.Controls.Add(btnFwd); bar.Controls.Add(btnBack);

            tabs.Dock = DockStyle.Fill;
            tabs.Multiline = true;          // ← タブが入りきらないと2段・3段に折り返す
            tabs.ShowToolTips = true;
            tabs.Padding = new Point(10, 4);
            tabs.Font = new Font("Yu Gothic UI", 9.5f);
            tabs.SelectedIndexChanged += (s, e) => SyncAddress();
                        tabs.MouseDoubleClick += (s, e) => { if (e.Button == MouseButtons.Left) CloseTab(TabAt(e.Location)); };

            Controls.Add(tabs);
            Controls.Add(bar);

            if (args.Length == 0) NewTab(startDir);
            foreach (var a in args) NewTab(a);

            Application.AddMessageFilter(this);
            FormClosing += (s, e) => { foreach (TabPage t in tabs.TabPages) ((BrowserPage)t).Close(); };
        }

        Button MakeBtn(string text, string tip, EventHandler h)
        {
            var b = new Button { Text = text, Width = 34, FlatStyle = FlatStyle.System };
            b.Click += h;
            new ToolTip().SetToolTip(b, tip);
            return b;
        }

        void Cur(Action<BrowserPage> f) { var p = tabs.SelectedTab as BrowserPage; if (p != null) f(p); }
        string CurPath() { var p = tabs.SelectedTab as BrowserPage; return p != null && p.CurrentPath != "" ? p.CurrentPath : startDir; }

        BrowserPage TabAt(Point pt)
        {
            for (int i = 0; i < tabs.TabCount; i++) if (tabs.GetTabRect(i).Contains(pt)) return (BrowserPage)tabs.TabPages[i];
            return null;
        }

        void NewTab(string path)
        {
            var p = new BrowserPage(path);
            p.Navigated += (s, e) => { if (tabs.SelectedTab == s) SyncAddress(); };
            tabs.TabPages.Add(p);
            tabs.SelectedTab = p;
            var h = p.Handle; // ここで ExplorerBrowser を作る
        }

        void CloseTab(BrowserPage p)
        {
            if (p == null) return;
            if (tabs.TabCount == 1) { Close(); return; }
            int i = tabs.TabPages.IndexOf(p);
            p.Close();
            tabs.TabPages.Remove(p);
            p.Dispose();
            tabs.SelectedIndex = Math.Max(0, Math.Min(i, tabs.TabCount - 1));
        }

        void SyncAddress()
        {
            var p = tabs.SelectedTab as BrowserPage;
            address.Text = p != null ? p.CurrentPath : "";
            Text = (p != null ? p.Text + " - " : "") + "ExplorerTabs";
        }

        const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104, WM_MBUTTONUP = 0x208, WM_XBUTTONDOWN = 0x20B, WM_XBUTTONUP = 0x20C;
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
        bool IsMine(IntPtr h) { return h != IntPtr.Zero && GetAncestor(h, 2 /*GA_ROOT*/) == Handle; }
        public bool PreFilterMessage(ref Message m)
        {
            // マウスのサイドボタン：戻る（XBUTTON1）・進む（XBUTTON2）。中のフォルダ表示の上でも効くように、ここで拾う
            if (m.Msg == WM_XBUTTONUP && IsMine(m.HWnd))
            {
                int xb = (int)(((long)m.WParam >> 16) & 0xFFFF);
                if (xb == 1) { Cur(p => p.Back()); return true; }
                if (xb == 2) { Cur(p => p.Forward()); return true; }
            }
            if (m.Msg == WM_XBUTTONDOWN && IsMine(m.HWnd)) return true;  // 押した時は飲み込む（離した時に動かす）
            // タブの上で中ボタン → そのタブを閉じる（TabControl は中ボタンの MouseUp を出さないので、ここで拾う）
            if (m.Msg == WM_MBUTTONUP && m.HWnd == tabs.Handle)
            {
                long lp = (long)m.LParam;
                var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                var t = TabAt(pt);
                if (t != null) { BeginInvoke((Action)(() => CloseTab(t))); return true; } // メッセージの処理中に閉じると固まるので、処理が終わってから閉じる
            }
            if ((m.Msg != WM_KEYDOWN && m.Msg != WM_SYSKEYDOWN) || !ContainsFocus) return false;
            var k = (Keys)(int)m.WParam & Keys.KeyCode;
            var mod = Control.ModifierKeys;
            if (mod == Keys.Control && k == Keys.T) { NewTab(CurPath()); return true; }
            if (mod == Keys.Control && k == Keys.W) { var sel = tabs.SelectedTab as BrowserPage; BeginInvoke((Action)(() => CloseTab(sel))); return true; }
            if (mod == Keys.Control && k == Keys.L) { address.Focus(); address.SelectAll(); return true; }
            if (mod == Keys.Control && k == Keys.Tab) { if (tabs.TabCount > 0) tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.TabCount; return true; }
            if (mod == (Keys.Control | Keys.Shift) && k == Keys.Tab) { if (tabs.TabCount > 0) tabs.SelectedIndex = (tabs.SelectedIndex + tabs.TabCount - 1) % tabs.TabCount; return true; }
            if (mod == Keys.Alt && k == Keys.Left) { Cur(p => p.Back()); return true; }
            if (mod == Keys.Alt && k == Keys.Right) { Cur(p => p.Forward()); return true; }
            if (mod == Keys.Alt && k == Keys.Up) { Cur(p => p.Up()); return true; }
            var cur = tabs.SelectedTab as BrowserPage;
            if (cur == null || !cur.ContainsFocus) return false;
            // Del では消さない（警告だけ）。消すのは右クリックの「削除」からだけ（うっかり消し防止）。
            // Shift+Del（ごみ箱を通さず消す）・Ctrl+D（エクスプローラーの削除）も同じく止める。名前の変更中（Edit）の Del は字を消すのでそのまま
            if ((k == Keys.Delete || (mod == Keys.Control && k == Keys.D)) && !FocusIsEdit()) { WarnDel(); return true; }
            // それ以外のキーは中のフォルダ表示へ渡す（Ctrl+C/V/X/A/Z・F2 など。アドレス欄の Ctrl+C は欄のまま）
            if (cur.TranslateKey(ref m)) return true;
            return false;
        }

        [DllImport("user32.dll")] static extern IntPtr GetFocus();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        static bool FocusIsEdit()
        {
            var sb = new StringBuilder(64);
            GetClassName(GetFocus(), sb, 64);
            return sb.ToString() == "Edit";
        }

        ToolTip delTip = new ToolTip();
        void WarnDel()
        {
            System.Media.SystemSounds.Exclamation.Play();
            delTip.Show("Del キーでは消しません。消す時は右クリック →「削除」", address, 0, address.Height + 2, 3000);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
