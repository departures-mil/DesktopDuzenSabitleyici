using DDS.Core;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DDS;

/// <summary>
/// Large preview of one saved record: the screens it belongs to, drawn to scale, with every icon where it was saved.
/// It can also show how the record would land on the screens connected right now.
/// </summary>
public sealed class PreviewForm : Form
{
    readonly StoreData store;
    readonly List<string> ids;
    readonly Func<string> currentKey;
    readonly Action<string> restore;
    int index;
    bool showOnCurrent;

    readonly Panel header, footer;
    readonly LayoutPreview canvas;
    readonly Label lTitle, lBadge, lInfo, lLegend, lHint;
    readonly Button bSaved, bHere, bPrev, bNext, bRestore, bClose;

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public PreviewForm(StoreData store, List<string> ids, int index, Func<string> currentKey, Action<string> restore)
    {
        this.store = store;
        this.ids = ids;
        this.index = Math.Clamp(index, 0, Math.Max(0, ids.Count - 1));
        this.currentKey = currentKey;
        this.restore = restore;

        Text = Lang.T("pv_title");
        Icon = Theme.AppIcon();
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.F(10f);
        HandleCreated += (_, _) => Theme.DarkTitle(this);
        ShowInTaskbar = false;
        KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(D(700), D(480));
        RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No;

        // ---- header: which screen this record belongs to ----
        header = new Panel { Dock = DockStyle.Top, BackColor = Theme.Card, Padding = new Padding(D(20), D(14), D(20), D(10)), AutoSize = true };
        var hf = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Card };
        lTitle = new Label { AutoSize = true, Font = Theme.F(15f, true), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 4) };
        lBadge = new Label { AutoSize = true, Font = Theme.F(10.5f, true), Padding = new Padding(D(10), D(3), D(10), D(3)), Margin = new Padding(0, 2, 0, 6) };
        lInfo = new Label { AutoSize = true, ForeColor = Theme.Sub, Margin = new Padding(0, 0, 0, 4) };
        lLegend = new Label { AutoSize = true, ForeColor = Theme.Text, Font = Theme.F(9.5f), Margin = new Padding(0, 0, 0, 6) };
        var modes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = Padding.Empty };
        bSaved = Theme.ActionButton("", Btn.Primary);
        bHere = Theme.ActionButton("", Btn.Secondary);
        bSaved.Click += (_, _) => { showOnCurrent = false; ShowRecord(); };
        bHere.Click += (_, _) => { showOnCurrent = true; ShowRecord(); };
        modes.Controls.AddRange(new Control[] { bSaved, bHere });
        hf.Controls.AddRange(new Control[] { lTitle, lBadge, lInfo, lLegend, modes });
        header.Controls.Add(hf);

        // ---- footer: navigation and restore ----
        footer = new Panel { Dock = DockStyle.Bottom, Height = D(64), BackColor = Theme.Card, Padding = new Padding(D(16), D(6), D(16), D(6)) };
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, BackColor = Theme.Card };
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Card };
        bPrev = Theme.ActionButton("", Btn.Secondary);
        bNext = Theme.ActionButton("", Btn.Secondary);
        bPrev.Click += (_, _) => Step(-1);
        bNext.Click += (_, _) => Step(+1);
        lHint = new Label { AutoSize = true, ForeColor = Theme.Sub, Font = Theme.F(9f), Margin = new Padding(D(6), D(16), 0, 0) };
        left.Controls.AddRange(new Control[] { bPrev, bNext, lHint });
        bRestore = Theme.ActionButton("", Btn.Ok);
        bClose = Theme.ActionButton("", Btn.Secondary);
        bRestore.Click += (_, _) => { if (CurrentId is string id) { restore(id); Close(); } };
        bClose.Click += (_, _) => Close();
        right.Controls.AddRange(new Control[] { bRestore, bClose });
        footer.Controls.Add(left);
        footer.Controls.Add(right);

        // ---- the drawing ----
        canvas = new LayoutPreview(store, currentKey) { Dock = DockStyle.Fill };

        Controls.Add(canvas);
        Controls.Add(footer);
        Controls.Add(header);

        bPrev.Text = "◀  " + Lang.T("pv_prev");
        bNext.Text = Lang.T("pv_next") + "  ▶";
        lHint.Text = Lang.T("pv_hint");
        bRestore.Text = Lang.T("pv_restore");
        bClose.Text = Lang.T("pv_close");
        bSaved.Text = Lang.T("pv_mode_saved");
        bHere.Text = Lang.T("pv_mode_here");
        AcceptButton = null;
        CancelButton = bClose;

        ShowRecord();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var wa = (Owner != null ? Screen.FromControl(Owner) : Screen.FromPoint(Cursor.Position)).WorkingArea;
        int w = wa.Width * 92 / 100, h = wa.Height * 92 / 100;
        Bounds = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { } // dark title bar
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Left) { Step(Lang.IsRtl ? +1 : -1); return true; }
        if (keyData == Keys.Right) { Step(Lang.IsRtl ? -1 : +1); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    string? CurrentId => index >= 0 && index < ids.Count ? ids[index] : null;

    void Step(int d)
    {
        int n = index + d;
        if (n < 0 || n >= ids.Count) return;
        index = n;
        ShowRecord();
    }

    // ---------------- texts ----------------

    static string MonText(MonitorRec m, int i) =>
        $"{i + 1})  {m.W}×{m.H}   %{m.Dpi * 100 / 96}   {m.Model}{(m.Primary ? "   — " + Lang.T("primary") : "")}";

    static List<MonitorRec> MonsOf(Profile p) => p.Monitors.Count > 0 ? p.Monitors : StoreData.MonitorsFromKey(p.Key);

    void ShowRecord()
    {
        canvas.SnapId = CurrentId;
        var f = CurrentId is string id ? store.Find(id) : null;
        bPrev.Enabled = index > 0;
        bNext.Enabled = index < ids.Count - 1;
        bRestore.Enabled = f != null;
        if (f == null)
        {
            lTitle.Text = Lang.T("log_noLayout");
            lBadge.Text = ""; lInfo.Text = ""; lLegend.Text = "";
            canvas.Invalidate();
            return;
        }
        var (p, s) = f.Value;
        string cur = currentKey();
        bool isCurrent = p.Key == cur;
        var curMons = DisplayInfo.GetMonitors();
        bool sameScreens = isCurrent || DDS.LayoutEngine.SameScreens(p, curMons);

        lTitle.Text = p.Title.Replace("  +  ", "   +   ");
        if (isCurrent)
        {
            lBadge.Text = "✔  " + Lang.T("pv_is_current");
            lBadge.BackColor = Look.Tok("ok-soft", Theme.Bg);
            lBadge.ForeColor = Look.Tok("ok-text", Theme.Bg);
        }
        else if (sameScreens)
        {
            lBadge.Text = "●  " + Lang.T("pv_same_screens");
            lBadge.BackColor = Look.Tok("accent-soft", Theme.Bg);
            lBadge.ForeColor = Look.Tok("badge-text", Theme.Bg);
        }
        else
        {
            lBadge.Text = "⚠  " + Lang.T("pv_other");
            lBadge.BackColor = Look.Tok("warn-soft", Theme.Bg);
            lBadge.ForeColor = Look.Tok("warn-text", Theme.Bg);
        }

        int pos = p.History.IndexOf(s) + 1;
        lInfo.Text = Lang.F("pv_info", s.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), s.Icons.Count,
            Lang.T("src_" + s.Source), pos, p.History.Count, index + 1, ids.Count);

        var mons = MonsOf(p);
        var lines = new List<string> { Lang.T(showOnCurrent && !isCurrent ? "pv_legend_now" : "pv_legend_saved") };
        if (showOnCurrent && !isCurrent)
            lines.AddRange(curMons.Select((m, i) => "    " + MonText(MonitorRec.From(m), i)));
        else
            for (int i = 0; i < mons.Count; i++)
            {
                var m = mons[i];
                int n = s.Icons.Count(e => m.Bounds.Contains(e.X, e.Y));
                lines.Add("    " + MonText(m, i) + "   ·   " + Lang.F("pv_icons_on", n));
            }
        lLegend.Text = string.Join("\n", lines);

        // The "on this screen" view only makes sense for a record of another display setup.
        bHere.Visible = !isCurrent;
        if (isCurrent) showOnCurrent = false;
        ((FcButton)bSaved).Kind = showOnCurrent ? Btn.Secondary : Btn.Primary;
        ((FcButton)bHere).Kind = showOnCurrent ? Btn.Primary : Btn.Secondary;
        bSaved.Invalidate(); bHere.Invalidate();
        bSaved.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bSaved.BackColor, 0.25f);
        bHere.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bHere.BackColor, 0.25f);
        canvas.ShowOnCurrent = showOnCurrent;
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

/// <summary>Real icons and display names of desktop items, looked up from their parsing names (paths or ::{GUID}).</summary>
static class IconCache
{
    static readonly Dictionary<string, (Bitmap? img, string name)> cache = new(StringComparer.OrdinalIgnoreCase);

    public static (Bitmap? img, string name) Get(string key)
    {
        if (cache.TryGetValue(key, out var v)) return v;
        Bitmap? img = null;
        string name = Fallback(key);
        IntPtr pidl = IntPtr.Zero;
        try
        {
            if (SHParseDisplayName(key, IntPtr.Zero, out pidl, 0, out _) == 0 && pidl != IntPtr.Zero)
            {
                var info = new SHFILEINFO();
                var r = SHGetFileInfoPidl(pidl, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON | SHGFI_DISPLAYNAME);
                if (r != IntPtr.Zero)
                {
                    if (!string.IsNullOrWhiteSpace(info.szDisplayName)) name = info.szDisplayName;
                    if (info.hIcon != IntPtr.Zero)
                    {
                        try { using var ic = Icon.FromHandle(info.hIcon); img = ic.ToBitmap(); }
                        finally { DestroyIcon(info.hIcon); }
                    }
                }
            }
        }
        catch { }
        finally { if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
        cache[key] = (img, name);
        return (img, name);
    }

    static string Fallback(string key)
    {
        if (key.StartsWith("::")) return "?";
        try
        {
            var ext = Path.GetExtension(key).ToLowerInvariant();
            return ext is ".lnk" or ".url" ? Path.GetFileNameWithoutExtension(key) : Path.GetFileName(key);
        }
        catch { return key; }
    }

    const uint SHGFI_ICON = 0x100, SHGFI_DISPLAYNAME = 0x200, SHGFI_LARGEICON = 0x0, SHGFI_PIDL = 0x8;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHParseDisplayName(string name, IntPtr bindCtx, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
    static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint attrs, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
}
