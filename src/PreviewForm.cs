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

    readonly Panel header, canvas, footer;
    readonly Label lTitle, lBadge, lInfo, lLegend, lHint;
    readonly Button bSaved, bHere, bPrev, bNext, bRestore, bClose;
    readonly ToolTip tip = new() { InitialDelay = 150, ReshowDelay = 50 };
    readonly List<(RectangleF r, string name)> hits = new();
    string? lastTip;

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
        Font = new Font("Segoe UI", 10f);
        ShowInTaskbar = false;
        KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(D(700), D(480));
        RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No;

        // ---- header: which screen this record belongs to ----
        header = new Panel { Dock = DockStyle.Top, BackColor = Theme.Card, Padding = new Padding(D(20), D(14), D(20), D(10)), AutoSize = true };
        var hf = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Card };
        lTitle = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 15f), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 4) };
        lBadge = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5f), Padding = new Padding(D(10), D(3), D(10), D(3)), Margin = new Padding(0, 2, 0, 6) };
        lInfo = new Label { AutoSize = true, ForeColor = Theme.Sub, Margin = new Padding(0, 0, 0, 4) };
        lLegend = new Label { AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 6) };
        var modes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = Padding.Empty };
        bSaved = Theme.ActionButton("", Theme.Accent);
        bHere = Theme.ActionButton("", Theme.Card2);
        bSaved.Click += (_, _) => { showOnCurrent = false; ShowRecord(); };
        bHere.Click += (_, _) => { showOnCurrent = true; ShowRecord(); };
        modes.Controls.AddRange(new Control[] { bSaved, bHere });
        hf.Controls.AddRange(new Control[] { lTitle, lBadge, lInfo, lLegend, modes });
        header.Controls.Add(hf);

        // ---- footer: navigation and restore ----
        footer = new Panel { Dock = DockStyle.Bottom, Height = D(64), BackColor = Theme.Card, Padding = new Padding(D(16), D(6), D(16), D(6)) };
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, BackColor = Theme.Card };
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Card };
        bPrev = Theme.ActionButton("", Theme.Card2);
        bNext = Theme.ActionButton("", Theme.Card2);
        bPrev.Click += (_, _) => Step(-1);
        bNext.Click += (_, _) => Step(+1);
        lHint = new Label { AutoSize = true, ForeColor = Theme.Sub, Font = new Font("Segoe UI", 9f), Margin = new Padding(D(6), D(16), 0, 0) };
        left.Controls.AddRange(new Control[] { bPrev, bNext, lHint });
        bRestore = Theme.ActionButton("", Theme.Green);
        bClose = Theme.ActionButton("", Theme.Card2);
        bRestore.Click += (_, _) => { if (CurrentId is string id) { restore(id); Close(); } };
        bClose.Click += (_, _) => Close();
        right.Controls.AddRange(new Control[] { bRestore, bClose });
        footer.Controls.Add(left);
        footer.Controls.Add(right);

        // ---- the drawing ----
        canvas = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, RightToLeft = RightToLeft.No };
        canvas.Paint += PaintCanvas;
        canvas.Resize += (_, _) => canvas.Invalidate();
        canvas.MouseMove += CanvasMouseMove;
        canvas.MouseLeave += (_, _) => { tip.Hide(canvas); lastTip = null; };

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
        bool sameScreens = isCurrent || LayoutEngine.SameScreens(p, curMons);

        lTitle.Text = (p.Display.Length > 0 ? p.Display : p.Key).Replace("  +  ", "   +   ");
        if (isCurrent)
        {
            lBadge.Text = "✔  " + Lang.T("pv_is_current");
            lBadge.BackColor = Color.FromArgb(20, 70, 45);
            lBadge.ForeColor = Theme.Green;
        }
        else if (sameScreens)
        {
            lBadge.Text = "●  " + Lang.T("pv_same_screens");
            lBadge.BackColor = Color.FromArgb(25, 50, 90);
            lBadge.ForeColor = Color.FromArgb(147, 197, 253);
        }
        else
        {
            lBadge.Text = "⚠  " + Lang.T("pv_other");
            lBadge.BackColor = Color.FromArgb(80, 55, 15);
            lBadge.ForeColor = Color.FromArgb(251, 191, 36);
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
        bSaved.BackColor = showOnCurrent ? Theme.Card2 : Theme.Accent;
        bHere.BackColor = showOnCurrent ? Theme.Accent : Theme.Card2;
        bSaved.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bSaved.BackColor, 0.25f);
        bHere.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bHere.BackColor, 0.25f);
        canvas.Invalidate();
    }

    // ---------------- drawing ----------------

    void PaintCanvas(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        hits.Clear();

        if (CurrentId is not string id || store.Find(id) is not { } f) return;
        var (p, s) = f;
        bool onCurrent = showOnCurrent && p.Key != currentKey();

        List<MonitorRec> mons;
        List<(string key, int x, int y)> icons;
        Size spacing;
        if (onCurrent)
        {
            var cur = DisplayInfo.GetMonitors();
            mons = cur.Select(MonitorRec.From).ToList();
            spacing = DesktopIcons.LastSpacing;
            List<IconPos> t;
            try { t = LayoutEngine.Build(p, s, cur, spacing, out _); } catch { t = new(); }
            icons = t.Select(i => (i.Key, i.X, i.Y)).ToList();
        }
        else
        {
            mons = MonsOf(p);
            int dpi = mons.FirstOrDefault(m => m.Primary)?.Dpi ?? 96;
            spacing = s.SpacingW > 10 && s.SpacingH > 10 ? new Size(s.SpacingW, s.SpacingH)
                : new Size(76 * dpi / 96, 106 * dpi / 96);
            icons = s.Icons.Select(i => (i.Key, i.X, i.Y)).ToList();
        }
        if (mons.Count == 0) return;

        // Fit the whole virtual desktop (all monitors) into the canvas.
        var vb = mons.Select(m => m.Bounds).Aggregate(Rectangle.Union);
        float pad = D(28);
        float avW = canvas.ClientSize.Width - 2 * pad, avH = canvas.ClientSize.Height - 2 * pad;
        if (avW < 50 || avH < 50) return;
        float k = Math.Min(avW / vb.Width, avH / vb.Height);
        float ox = pad + (avW - vb.Width * k) / 2 - vb.X * k, oy = pad + (avH - vb.Height * k) / 2 - vb.Y * k;
        RectangleF Map(Rectangle r) => new(ox + r.X * k, oy + r.Y * k, r.Width * k, r.Height * k);

        // monitors
        for (int i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            var r = Map(m.Bounds);
            var bezel = RectangleF.Inflate(r, D(6), D(6));
            using (var bz = Rounded(bezel, D(8))) using (var bb = new SolidBrush(Color.FromArgb(5, 8, 18))) g.FillPath(bb, bz);
            using (var wall = new LinearGradientBrush(r, Color.FromArgb(30, 64, 120), Color.FromArgb(12, 28, 60), 60f)) g.FillRectangle(wall, r);

            // big faint number in the middle, like Windows "Identify"
            using (var nf = new Font("Segoe UI Light", Math.Max(12f, Math.Min(r.Height * 0.45f, r.Width * 0.3f)), GraphicsUnit.Pixel))
            using (var nb = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString((i + 1).ToString(), nf, nb, r, sf);

            // taskbar strip (the part of the screen outside the work area), with the screen's label inside it
            var w = Map(m.Work);
            var strips = new List<RectangleF>();
            if (w.Bottom < r.Bottom - 1) strips.Add(RectangleF.FromLTRB(r.Left, w.Bottom, r.Right, r.Bottom));
            if (w.Top > r.Top + 1) strips.Add(RectangleF.FromLTRB(r.Left, r.Top, r.Right, w.Top));
            if (w.Left > r.Left + 1) strips.Add(RectangleF.FromLTRB(r.Left, r.Top, w.Left, r.Bottom));
            if (w.Right < r.Right - 1) strips.Add(RectangleF.FromLTRB(w.Right, r.Top, r.Right, r.Bottom));
            using (var tb = new SolidBrush(Color.FromArgb(225, 14, 18, 30))) foreach (var st in strips) g.FillRectangle(tb, st);

            // label pill: top-left corner outside the icons' usual area would be ideal, so use the bottom-center
            string label = $"{i + 1}  ·  {m.W}×{m.H}  %{m.Dpi * 100 / 96}  {m.Model}{(m.Primary ? "  · " + Lang.T("primary") : "")}";
            using (var lf = new Font("Segoe UI Semibold", Math.Clamp(r.Height * 0.03f, 9f, 15f), GraphicsUnit.Pixel))
            {
                var sz = g.MeasureString(label, lf);
                var area = strips.Count > 0 && strips[0].Height >= sz.Height ? strips[0] : r;
                var pill = new RectangleF(area.Left + (area.Width - sz.Width) / 2 - D(8),
                    area == r ? r.Bottom - sz.Height - D(10) : area.Top + (area.Height - sz.Height) / 2 - D(2),
                    sz.Width + D(16), sz.Height + D(4));
                using var pp = Rounded(pill, pill.Height / 2);
                using var pb = new SolidBrush(m.Primary ? Color.FromArgb(230, 37, 99, 235) : Color.FromArgb(230, 51, 65, 85));
                g.FillPath(pb, pp);
                TextRenderer.DrawText(g, label, lf, Rectangle.Round(pill), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            using var border = new Pen(m.Primary ? Theme.Accent : Color.FromArgb(71, 85, 105), m.Primary ? D(2) : D(1));
            g.DrawRectangle(border, r.X, r.Y, r.Width, r.Height);
        }

        // icons
        float cw = spacing.Width * k, ch = spacing.Height * k;
        float isz = Math.Max(6f, Math.Min(cw * 0.55f, ch * 0.42f));
        float fontPx = Math.Clamp(ch * 0.12f, 6f, 13f);
        bool labels = ch >= 34;
        using var font = new Font("Segoe UI", fontPx, GraphicsUnit.Pixel);
        using var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        using var sfl = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
        int outside = 0;
        foreach (var (key, x, y) in icons)
        {
            if (!mons.Any(m => m.Bounds.Contains(x + 4, y + 4))) outside++;
            var (img, name) = IconCache.Get(key);
            float cx = ox + x * k, cy = oy + y * k;
            var ir = new RectangleF(cx + (cw - isz) / 2, cy + ch * 0.06f, isz, isz);
            if (img != null) g.DrawImage(img, ir);
            else
            {
                using var gp = Rounded(ir, isz * 0.18f);
                using var gb = new SolidBrush(Color.FromArgb(200, 148, 163, 184));
                g.FillPath(gb, gp);
            }
            if (labels)
            {
                var tr = new RectangleF(cx + 1, ir.Bottom + ch * 0.03f, cw - 2, fontPx * 2.5f);
                var tr2 = tr; tr2.Offset(1, 1);
                g.DrawString(name, font, shadow, tr2, sfl);
                g.DrawString(name, font, Brushes.White, tr, sfl);
            }
            hits.Add((new RectangleF(cx, cy, cw, ch), name));
        }

        if (outside > 0)
        {
            string warn = "⚠  " + Lang.F("pv_outside", outside);
            using var wf = new Font("Segoe UI Semibold", 10f);
            TextRenderer.DrawText(g, warn, wf, new Point(D(12), D(6)), Color.FromArgb(251, 191, 36));
        }
    }

    void CanvasMouseMove(object? sender, MouseEventArgs e)
    {
        string? t = null;
        for (int i = hits.Count - 1; i >= 0; i--)
            if (hits[i].r.Contains(e.Location)) { t = hits[i].name; break; }
        if (t == lastTip) return;
        lastTip = t;
        if (t == null) tip.Hide(canvas);
        else tip.Show(t, canvas, e.X + D(14), e.Y + D(18), 4000);
    }

    static GraphicsPath Rounded(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Math.Max(1f, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tip.Dispose();
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
    }
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
