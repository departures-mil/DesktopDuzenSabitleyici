using System.Text.RegularExpressions;

namespace DDS.Core;

/// <summary>
/// Çökme raporu (web FC.crash / fccore.crash ile aynı kurallar): beklenmeyen hata → admin gelen kutusu, tur:"cokme".
/// Aynı hata imzası (sürüm başına) bir kez; günde en çok 5 rapor. Yalnızca hata metni, yığın, uygulama, sürüm, platform gider;
/// e-posta, kullanıcı klasörü, URL sorguları, uzun sayılar ve tırnak içi veriler maskelenir.
/// </summary>
public static class Crash
{
    public const int MaxDay = 5;
    static readonly Regex Ignore = new(@"ResizeObserver loop|^Script error\.?$|Failed to fetch|NetworkError|Load failed|network error|AbortError|The user aborted|permission-denied|QuotaExceeded|ERR_INTERNET_DISCONNECTED|TaskCanceledException|OperationCanceledException", RegexOptions.IgnoreCase);
    public static Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;
    public static Func<string> Version { get; set; } = () => "";
    /// <summary>Gönderim (varsayılan: Inbox.Post("cokme")). Test için değiştirilebilir.</summary>
    public static Func<string, Task>? Sender { get; set; }

    static readonly Regex Email = new(@"[\w.+-]+@[\w-]+(\.[\w-]+)+");
    static readonly Regex UrlQ = new(@"(https?://[^\s?#'"")]+)[?#][^\s'"")]*");
    static readonly Regex WinUser = new(@"([A-Za-z]:[\\/]Users[\\/])[^\\/\s]+", RegexOptions.IgnoreCase);
    static readonly Regex NixUser = new(@"(/(?:home|Users)/)[^/\s]+");
    static readonly Regex Android = new(@"(/storage/emulated/\d+/)\S*");
    static readonly Regex LongNum = new(@"\d{6,}");
    static readonly Regex Quoted = new(@"([""'`])((?:(?!\1).){0,400})\1");
    static readonly Regex SafeInner = new(@"^[\w$.\-\[\]]{0,40}$");

    public static string Sanitize(string? s, int max = 600)
    {
        s ??= "";
        s = Email.Replace(s, "<email>");
        s = UrlQ.Replace(s, "$1");
        s = WinUser.Replace(s, "$1<user>");
        s = NixUser.Replace(s, "$1<user>");
        s = Android.Replace(s, "$1<path>");
        s = LongNum.Replace(s, "<n>");
        s = Quoted.Replace(s, m => SafeInner.IsMatch(m.Groups[2].Value) ? m.Value : m.Groups[1].Value + "…" + m.Groups[1].Value);
        return s.Length > max ? s[..max] : s;
    }

    /// <summary>djb2 (JS charCodeAt = UTF-16 kod birimleri, C# char ile aynı).</summary>
    public static string Hash(string s)
    {
        uint h = 5381;
        foreach (char c in s) h = unchecked((h << 5) + h + c);
        return h.ToString("x");
    }

    /// <summary>İmza: maskelenmiş ileti + en içteki iki çerçeve (tür.yöntem; satır numarası yok).</summary>
    public static string Signature(string msg, IEnumerable<string> frames) =>
        Hash(Sanitize(msg, 300) + "|" + string.Join("|", frames.Take(2)));

    public static List<string> Frames(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException != null) inner = inner.InnerException;
        var st = new System.Diagnostics.StackTrace(inner, false);
        var list = new List<string>();
        foreach (var f in st.GetFrames() ?? Array.Empty<System.Diagnostics.StackFrame>())
        {
            var m = f.GetMethod();
            if (m != null) list.Add((m.DeclaringType?.Name ?? "?") + "." + m.Name);
        }
        return list;
    }

    static string Day() => Now().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static bool Enabled
    {
        get => CoreState.Get("crash.enabled", true);
        set => CoreState.Set("crash.enabled", value);
    }

    public sealed class DayCount { public string d { get; set; } = ""; public int n { get; set; } }

    /// <summary>Göndermeli mi? Gönderecekse sayaçları günceller.</summary>
    public static bool Decide(string sig)
    {
        if (!Enabled) return false;
        var key = Version() + ":" + sig;
        var seen = CoreState.Get<List<string>>("crash.seen") ?? new();
        if (seen.Contains(key)) return false;
        var d = CoreState.Get<DayCount>("crash.day");
        var today = Day();
        if (d == null || d.d != today) d = new DayCount { d = today, n = 0 };
        if (d.n >= MaxDay) return false;
        d.n++;
        CoreState.Set("crash.day", d);
        seen.Add(key);
        CoreState.Set("crash.seen", seen.Skip(Math.Max(0, seen.Count - 200)).ToList());
        return true;
    }

    public static int SentToday()
    {
        var d = CoreState.Get<DayCount>("crash.day");
        return d != null && d.d == Day() ? d.n : 0;
    }

    /// <summary>Rapor metni ya da null (yok sayılır).</summary>
    public static (string sig, string text)? Build(Exception ex, string source)
    {
        var msg = ex.GetType().Name + ": " + ex.Message;
        if (ex.InnerException != null) msg += " → " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message;
        if (Ignore.IsMatch(msg) || ex is OperationCanceledException) return null;
        var sig = Signature(msg, Frames(ex));
        var stack = ex.ToString();
        var text = "[" + source + "] " + Sanitize(msg, 600) + "\n\n" + Sanitize(stack, 1800) + "\n\n#" + sig;
        return (sig, text);
    }

    /// <summary>Raporlar; gönderildiyse true. <paramref name="waitMs"/> &gt; 0 ise (süreç kapanırken) gönderimi o kadar bekler.</summary>
    public static bool Report(Exception? ex, string source, int waitMs = 0)
    {
        try
        {
            if (ex == null) return false;
            var r = Build(ex, source);
            if (r == null || !Decide(r.Value.sig)) return false;
            var task = Sender != null ? Sender(r.Value.text) : Inbox.Post("cokme", r.Value.text);
            if (waitMs > 0) task.Wait(waitMs);
            return true;
        }
        catch { return false; }
    }
}
