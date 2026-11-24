using System.Globalization;

namespace DDS.Core;

/// <summary>
/// Ortak çekirdek görünümü (web FC.look karşılığı): renk şeması (falcon varsayılan, klasik = v1.0.x renkleri, …),
/// koyu / açık / otomatik mod ve yazı boyutu (0.85–1.4). Tokenlar Generated.cs'den (← cekirdek/web/tools/schemes.js) gelir.
/// </summary>
public static class Look
{
    public const double MinScale = 0.85, MaxScale = 1.4, Step = 0.05;

    public static string Scheme { get; private set; } = "falcon";
    /// <summary>dark | light | auto</summary>
    public static string Mode { get; private set; } = "dark";
    public static double Scale { get; private set; } = 1.0;
    /// <summary>Windows "uygulama modu" açık mı (auto için). Test için değiştirilebilir.</summary>
    public static Func<bool> SystemLight { get; set; } = () => false;

    public static event Action? Changed;

    public static bool IsDark => Effective == "dark";
    public static string Effective => Mode == "auto" ? (SafeLight() ? "light" : "dark") : Mode;
    static bool SafeLight() { try { return SystemLight(); } catch { return false; } }

    public static void Set(string? scheme, string? mode, double? scale, bool notify = true)
    {
        if (scheme != null) Scheme = CoreTokens.Schemes.ContainsKey(scheme) ? scheme : "falcon";
        if (mode != null) Mode = mode is "dark" or "light" or "auto" ? mode : "dark";
        if (scale != null) Scale = Clamp(scale.Value);
        if (notify) Changed?.Invoke();
    }

    public static double Clamp(double x) => double.IsNaN(x) ? 1 : Math.Round(Math.Min(MaxScale, Math.Max(MinScale, x)), 2);

    public static string SchemeLabel(string id) => CoreStrings.T("fc.scheme." + id);

    // ---------------- tokens ----------------

    public static string Raw(string token)
    {
        var set = CoreTokens.Schemes.TryGetValue(Scheme, out var s) ? s : CoreTokens.Schemes["falcon"];
        var d = set[IsDark ? 0 : 1];
        return d.TryGetValue(token, out var v) ? v : "#ff00ff";
    }

    /// <summary>Token rengi; saydam tonlar <paramref name="over"/> (varsayılan: bg) üzerine karıştırılıp opak verilir (WinForms saydamlık bilmez).</summary>
    public static Color Tok(string token, Color? over = null)
    {
        var c = Parse(Raw(token));
        if (c.A == 255) return c;
        var b = over ?? Parse(Raw("bg"));
        return Blend(c, b);
    }

    public static Color Blend(Color top, Color under)
    {
        double a = top.A / 255.0;
        int Mix(int t, int u) => (int)Math.Round(t * a + u * (1 - a));
        return Color.FromArgb(255, Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

    public static Color Mix(Color a, Color b, double t) =>
        Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>#rgb, #rgba, #rrggbb, #rrggbbaa (CSS sırası: alfa en sonda).</summary>
    public static Color Parse(string hex)
    {
        var h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length is 3 or 4) h = string.Concat(h.Select(ch => new string(ch, 2)));
        if (h.Length == 6) h += "ff";
        if (h.Length != 8) return Color.Magenta;
        int P(int i) => int.Parse(h.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(P(6), P(0), P(2), P(4));
    }

    public static int Px(string token)
    {
        var v = Raw(token).Replace("px", "");
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 1;
    }

    /// <summary>Bir zemin üzerinde okunabilir metin rengi (WCAG göreli parlaklık).</summary>
    public static Color OnColor(Color bg) => Luminance(bg) > 0.45 ? Color.FromArgb(15, 23, 42) : Color.White;

    public static double Luminance(Color c)
    {
        static double L(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * L(c.R) + 0.7152 * L(c.G) + 0.0722 * L(c.B);
    }

    public static double Contrast(Color a, Color b)
    {
        double x = Luminance(a) + 0.05, y = Luminance(b) + 0.05;
        return x > y ? x / y : y / x;
    }
}
