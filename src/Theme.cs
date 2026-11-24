using System.Drawing.Drawing2D;
using DDS.Core;

namespace DDS;

public enum Btn { Primary, Secondary, Ok, Danger, Accent2, Ghost }

/// <summary>
/// Uygulamanın renkleri ve denetim görünümü — v1.1.0'dan beri ortak çekirdek tokenlarından (Look) gelir.
/// Adlar eski (1.0.x) adlarla aynı kaldı; değerler etkin şemaya / moda göre değişir. "klasik" şeması = 1.0.x renkleri.
/// </summary>
public static class Theme
{
    public static Color Bg => Look.Tok("bg");
    public static Color TitleBg => Look.Tok("topbar-bg");
    public static Color Card => Look.Tok("panel-solid");
    public static Color Card2 => Look.Tok("panel-hover");
    public static Color Text => Look.Tok("text");
    public static Color Sub => Look.Tok("muted");
    public static Color Label => Look.Tok("label");
    public static Color Accent => Look.Tok("accent");
    public static Color Accent2 => Look.Tok("accent-2");
    public static Color Green => Look.Tok("ok");
    public static Color GreenText => Look.Tok("ok-text", Card);
    public static Color Red => Look.Tok("danger");
    public static Color RedText => Look.Tok("danger-text", Card);
    public static Color Warn => Look.Tok("warn-text", Card);
    public static Color Hover => Look.Tok("active-1", TitleBg);
    public static Color Border => Look.Tok("border", Card);
    public static Color BorderStrong => Look.Tok("border-strong", Card);
    public static Color Input => Look.IsDark ? Look.Mix(Card, Bg, 0.45) : Look.Tok("panel-hover");

    public const string IconFont = Fonts.IconFamily;
    public const string GDownload = "", GSync = "", GMin = "",
        GMax = "", GRestore = "", GClose = "", GFull = "", GBackFull = "",
        GMoon = "", GSun = "", GAuto = "", GGlobe = "";

    public static Font F(float size, bool semi = false) => Fonts.Ui(size, semi);

    public static Icon AppIcon()
    {
        try
        {
            using var s = typeof(Theme).Assembly.GetManifestResourceStream("DDS.app.ico");
            if (s != null) return new Icon(s);
        }
        catch { }
        return SystemIcons.Application;
    }

    /// <summary>Falcon düğmesi. Eski çağrılar (renk ile) türüne çevrilir.</summary>
    public static Button ActionButton(string text, Color back)
    {
        var kind = back == Accent ? Btn.Primary : back == Green ? Btn.Ok : back == Red ? Btn.Danger : back == Card2 ? Btn.Secondary : Btn.Accent2;
        return ActionButton(text, kind);
    }

    public static Button ActionButton(string text, Btn kind) => new FcButton(kind)
    {
        Text = text, AutoSize = true, Font = F(10f, true), Cursor = Cursors.Hand,
        Padding = new Padding(12, 5, 12, 5), Margin = new Padding(0, 8, 10, 0),
    };

    /// <summary>Formu Falcon görünümüne getirir (iletişim kutuları için).</summary>
    public static void Dialog(Form f, float fontSize = 10f)
    {
        f.BackColor = Card;
        f.ForeColor = Text;
        f.Font = F(fontSize);
        f.RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        f.RightToLeftLayout = Lang.IsRtl;
        f.HandleCreated += (_, _) => DarkTitle(f);
    }

    /// <summary>Windows 10/11 başlık çubuğunu koyu temaya uydurur.</summary>
    public static void DarkTitle(Form f)
    {
        try
        {
            int on = Look.IsDark ? 1 : 0;
            Native.Win32.DwmSetWindowAttribute(f.Handle, 20, ref on, 4);
        }
        catch { }
    }

    public static TextBox Box(bool multi = false) => new()
    {
        Multiline = multi, ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None,
        BackColor = Input, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle,
    };

    public static ComboBox Combo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Input, ForeColor = Text,
    };

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>Falcon .btn: yuvarlak köşe; birincil = primary-1 → primary-2 geçişli, diğerleri yumuşak zemin + çizgi.</summary>
public sealed class FcButton : Button
{
    public Btn Kind { get; set; }
    bool hover, down;

    public FcButton(Btn kind)
    {
        Kind = kind;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var under = Parent?.BackColor ?? Theme.Card;
        g.Clear(under);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float rad = Math.Min(10f * DeviceDpi / 96f, Height / 2f);
        using var path = Theme.Round(r, rad);
        Color fg, line;
        Brush fill;
        switch (Kind)
        {
            case Btn.Primary:
                {
                    var c1 = Look.Tok("primary-1"); var c2 = Look.Tok("primary-2");
                    if (hover) { c1 = Look.Mix(c1, Color.White, 0.08); c2 = Look.Mix(c2, Color.White, 0.08); }
                    if (down) { c1 = Look.Mix(c1, Color.Black, 0.1); c2 = Look.Mix(c2, Color.Black, 0.1); }
                    fill = new LinearGradientBrush(new RectangleF(0, 0, Math.Max(1, Width), Math.Max(1, Height)), c1, c2, LinearGradientMode.Horizontal);
                    fg = Look.Tok("on-accent"); line = Color.Empty;
                    break;
                }
            case Btn.Ok:
                fill = new SolidBrush(Look.Tok(hover ? "ok-line" : "ok-soft", under)); fg = Look.Tok("ok-text", under); line = Look.Tok("ok-line", under); break;
            case Btn.Danger:
                fill = new SolidBrush(Look.Tok(hover ? "danger-line" : "danger-soft", under)); fg = Look.Tok("danger-text", under); line = Look.Tok("danger-line", under); break;
            case Btn.Accent2:
                fill = new SolidBrush(Look.Tok(hover ? "badge-line" : "badge-bg", under)); fg = Look.Tok("badge-text", under); line = Look.Tok("badge-line", under); break;
            case Btn.Ghost:
                fill = new SolidBrush(hover ? Look.Tok("active-1", under) : under); fg = Theme.Text; line = Color.Empty; break;
            default:
                fill = new SolidBrush(hover ? Look.Tok("active-1", Theme.Card2) : Theme.Card2); fg = Theme.Text; line = hover ? Theme.BorderStrong : Theme.Border; break;
        }
        if (!Enabled) fg = Look.Mix(fg, under, 0.5);
        g.FillPath(fill, path);
        fill.Dispose();
        if (line != Color.Empty)
        {
            using var pen = new Pen(line, Math.Max(1, Look.Px("border-w")));
            g.DrawPath(pen, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(Look.Tok("focus", under), 2);
            using var fp = Theme.Round(new RectangleF(1.5f, 1.5f, Width - 3.5f, Height - 3.5f), rad);
            g.DrawPath(pen, fp);
        }
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, flags);
    }
}

/// <summary>Falcon kartı: panel-solid zemin, yuvarlak köşe, ince çizgi.</summary>
public sealed class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float rad = 14f * DeviceDpi / 96f;
        using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), rad);
        using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
        using var pen = new Pen(Look.Tok("border", Parent?.BackColor ?? Theme.Bg), Math.Max(1, Look.Px("border-w")));
        g.DrawPath(pen, path);
    }
}
