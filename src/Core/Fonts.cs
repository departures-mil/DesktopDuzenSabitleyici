using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DDS.Core;

/// <summary>
/// Gömülü yazı tipleri (cekirdek/fonts: Inter 400/600/700 + Noto Sans Arabic 400/600/700, OFL).
/// PrivateFontCollection (GDI+) ve AddFontMemResourceEx (GDI — TextRenderer/denetimler) ikisine birden yüklenir.
/// Arapçada Noto Sans Arabic kullanılır. Yüklenemezse Segoe UI'ye düşer.
/// </summary>
public static class Fonts
{
    static readonly PrivateFontCollection Pfc = new();
    static readonly List<IntPtr> Mem = new();
    static bool loaded;
    static readonly Dictionary<string, FontFamily> Families = new(StringComparer.OrdinalIgnoreCase);

    [DllImport("gdi32.dll")]
    static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        var asm = typeof(Fonts).Assembly;
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("DDS.fonts.") && n.EndsWith(".ttf")))
        {
            try
            {
                using var s = asm.GetManifestResourceStream(name)!;
                var data = new byte[s.Length];
                s.ReadExactly(data);
                var ptr = Marshal.AllocCoTaskMem(data.Length);   // ömür boyu tutulur (GDI+ belleği okur)
                Marshal.Copy(data, 0, ptr, data.Length);
                Pfc.AddMemoryFont(ptr, data.Length);
                uint n = 0;
                AddFontMemResourceEx(ptr, (uint)data.Length, IntPtr.Zero, ref n);
                Mem.Add(ptr);
            }
            catch { }
        }
        foreach (var f in Pfc.Families) Families[f.Name] = f;
    }

    static FontFamily? Fam(string name) => Families.TryGetValue(name, out var f) ? f : null;

    /// <summary>Ana yazı tipi ailesi adı (dil ve ağırlığa göre).</summary>
    public static Font Ui(float size, bool semibold = false, bool bold = false)
    {
        Load();
        size = (float)Math.Round(size * Look.Scale, 1);
        bool ar = CoreStrings.Lang() == "ar";
        string baseName = ar ? "Noto Sans Arabic" : "Inter";
        try
        {
            if (semibold && Fam(baseName + " SemiBold") is { } sb) return new Font(sb, size, FontStyle.Regular, GraphicsUnit.Point);
            if (Fam(baseName) is { } f) return new Font(f, size, bold || semibold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        }
        catch { }
        return new Font(semibold ? "Segoe UI Semibold" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
    }

    public const string IconFamily = "Segoe MDL2 Assets";
    public static Font Icon(float size) => new(IconFamily, (float)Math.Round(size * Math.Min(1.2, Look.Scale), 1));
}
