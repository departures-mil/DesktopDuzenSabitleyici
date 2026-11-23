using System.Drawing.Drawing2D;

namespace DDS;

/// <summary>
/// Draws one saved record: its screens to scale with every icon where it was saved
/// (or, with ShowOnCurrent, how it would land on the screens connected now).
/// Used both inside the main window and in the large preview window.
/// </summary>
public sealed class LayoutPreview : Panel
{
    readonly StoreData store;
    readonly Func<string> currentKey;
    readonly ToolTip tip = new() { InitialDelay = 150, ReshowDelay = 50 };
    readonly List<(RectangleF r, string name)> hits = new();
    string? lastTip, snapId;
    bool showOnCurrent;

    /// <summary>Smaller margins for the embedded (main window) version.</summary>
    public bool Compact { get; set; }
    public string EmptyText { get; set; } = "";

    public string? SnapId { get => snapId; set { snapId = value; Invalidate(); } }
    public bool ShowOnCurrent { get => showOnCurrent; set { showOnCurrent = value; Invalidate(); } }

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public LayoutPreview(StoreData store, Func<string> currentKey)
    {
        this.store = store;
        this.currentKey = currentKey;
        BackColor = Theme.Bg;
        RightToLeft = RightToLeft.No;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        tip.Hide(this);
        lastTip = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tip.Dispose();
        base.Dispose(disposing);
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        hits.Clear();

        if (SnapId is not string id || store.Find(id) is not { } f)
        {
            if (EmptyText.Length > 0)
                TextRenderer.DrawText(g, EmptyText, Font, ClientRectangle, Theme.Sub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        var (p, s) = f;
        bool onCurrent = ShowOnCurrent && p.Key != currentKey();

        List<MonitorRec> mons;
        List<(string key, int x, int y)> icons;
        Size spacing;
        if (onCurrent)
        {
            var cur = DisplayInfo.GetMonitors();
            mons = cur.Select(MonitorRec.From).ToList();
            spacing = DesktopIcons.LastSpacing;
            List<IconPos> t;
            try { t = DDS.LayoutEngine.Build(p, s, cur, spacing, out _); } catch { t = new(); }
            icons = t.Select(i => (i.Key, i.X, i.Y)).ToList();
        }
        else
        {
            mons = p.Monitors.Count > 0 ? p.Monitors : StoreData.MonitorsFromKey(p.Key);
            int dpi = mons.FirstOrDefault(m => m.Primary)?.Dpi ?? 96;
            spacing = s.SpacingW > 10 && s.SpacingH > 10 ? new Size(s.SpacingW, s.SpacingH)
                : new Size(76 * dpi / 96, 106 * dpi / 96);
            icons = s.Icons.Select(i => (i.Key, i.X, i.Y)).ToList();
        }
        if (mons.Count == 0) return;

        // Fit the whole virtual desktop (all monitors) into the canvas.
        var vb = mons.Select(m => m.Bounds).Aggregate(Rectangle.Union);
        float pad = D(Compact ? 12 : 28);
        float avW = ClientSize.Width - 2 * pad, avH = ClientSize.Height - 2 * pad;
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? t = null;
        for (int i = hits.Count - 1; i >= 0; i--)
            if (hits[i].r.Contains(e.Location)) { t = hits[i].name; break; }
        if (t == lastTip) return;
        lastTip = t;
        if (t == null) tip.Hide(this);
        else tip.Show(t, this, e.X + D(14), e.Y + D(18), 4000);
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
}
