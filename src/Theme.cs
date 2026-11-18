namespace DDS;

public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(15, 23, 42);
    public static readonly Color TitleBg = Color.FromArgb(10, 16, 32);
    public static readonly Color Card = Color.FromArgb(30, 41, 59);
    public static readonly Color Card2 = Color.FromArgb(39, 52, 73);
    public static readonly Color Text = Color.FromArgb(226, 232, 240);
    public static readonly Color Sub = Color.FromArgb(148, 163, 184);
    public static readonly Color Accent = Color.FromArgb(59, 130, 246);
    public static readonly Color Green = Color.FromArgb(34, 197, 94);
    public static readonly Color Red = Color.FromArgb(239, 68, 68);
    public static readonly Color Hover = Color.FromArgb(45, 60, 85);

    public const string IconFont = "Segoe MDL2 Assets";
    public const string GDownload = "", GSync = "", GMin = "",
        GMax = "", GRestore = "", GClose = "";

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

    public static Button ActionButton(string text, Color back)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10f), Cursor = Cursors.Hand, Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(0, 8, 10, 0), UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.25f);
        return b;
    }
}
