using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DDS.Core;

/// <summary>
/// Admin gelen kutusu (SPEC "Admin gelen kutusu", web FC.contact ile aynı alanlar ve adres):
/// Firestore REST comm-calc-103111111 / mesajlar (yalnız ekleme), alanlar stringValue:
/// tur, uygulama, surum, platform, dil, mesaj, iletisim (isteğe bağlı), zaman (ISO), cihaz.
/// Gönderilemeyen yerel kuyruğa alınır (en çok 30), sonra tekrar denenir. Kullanıcı mesajında yedek FormSubmit.
/// </summary>
public static class Inbox
{
    public const string Url = "https://firestore.googleapis.com/v1/projects/comm-calc-103111111/databases/(default)/documents/mesajlar?key=AIzaSyB12RFS6ENPpmnnPsETfjedNH-3hmNWQvA";
    const string QKey = "contact.queue";
    const int MaxQ = 30;
    public static string App { get; set; } = "desktop-duzen-sabitleyici";
    public static Func<string> Version { get; set; } = () => "";
    /// <summary>Test için: (url, json gövde, başlıklar) → (durum, yanıt).</summary>
    public static Func<string, string, Dictionary<string, string>, Task<(int, string)>>? PostOverride { get; set; }
    static readonly SemaphoreSlim FlushGate = new(1, 1);
    public static event Action<int>? QueueChanged;

    public static string Platform => "windows/" + Environment.OSVersion.Version.ToString(3) + " · .NET";

    public static Dictionary<string, string> Fields(string tur, string mesaj, string? iletisim = null)
    {
        var f = new Dictionary<string, string>
        {
            ["tur"] = tur is "hata" or "cokme" ? tur : "oneri",
            ["uygulama"] = App,
            ["surum"] = Version(),
            ["platform"] = Platform,
            ["dil"] = CoreStrings.Lang(),
            ["mesaj"] = mesaj.Length > 5000 ? mesaj[..5000] : mesaj,
            ["zaman"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["cihaz"] = CoreState.DeviceId(),
        };
        if (!string.IsNullOrWhiteSpace(iletisim)) f["iletisim"] = iletisim.Trim().Length > 160 ? iletisim.Trim()[..160] : iletisim.Trim();
        return f;
    }

    public static string Payload(Dictionary<string, string> f) =>
        JsonSerializer.Serialize(new { fields = f.ToDictionary(k => k.Key, k => new Dictionary<string, string> { ["stringValue"] = k.Value }) });

    static async Task<(int, string)> Post(string url, string json, Dictionary<string, string> headers)
    {
        if (PostOverride != null) return await PostOverride(url, json, headers);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            req.Headers.UserAgent.ParseAdd("DesktopDuzenSabitleyici/" + Version());
            foreach (var h in headers) req.Headers.TryAddWithoutValidation(h.Key, h.Value);
            using var res = await http.SendAsync(req);
            return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        catch { return (0, ""); }
    }

    static async Task<bool> ToInbox(Dictionary<string, string> f)
    {
        var (st, _) = await Post(Url, Payload(f), new());
        return st is >= 200 and < 300;
    }

    static string MailTo() => new(Encoding.UTF8.GetString(Convert.FromBase64String("")).Reverse().ToArray());

    static async Task<bool> ToMail(Dictionary<string, string> f)
    {
        var body = new Dictionary<string, string>
        {
            ["_subject"] = "Desktop Simge Sabitleyici: " + f["tur"], ["_template"] = "table", ["_captcha"] = "false",
            ["Tur"] = f["tur"], ["Mesaj"] = f["mesaj"], ["Uygulama"] = f["uygulama"], ["Surum"] = f["surum"],
            ["Platform"] = f["platform"], ["Dil"] = f["dil"], ["Cihaz"] = f["cihaz"],
        };
        if (f.TryGetValue("iletisim", out var r))
        {
            body["Iletisim"] = r;
            if (r.Contains('@')) body["_replyto"] = r;
        }
        var (_, txt) = await Post("https://formsubmit.co/ajax/" + MailTo(), JsonSerializer.Serialize(body),
            new() { ["Accept"] = "application/json", ["Origin"] = "https://formsubmit.co", ["Referer"] = "https://formsubmit.co/" });
        try { return JsonNode.Parse(string.IsNullOrWhiteSpace(txt) ? "{}" : txt)?["success"]?.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) == true; }
        catch { return false; }
    }

    sealed class Item { public Dictionary<string, string> f { get; set; } = new(); public bool mail { get; set; } }

    static async Task<bool> Deliver(Item it) => await ToInbox(it.f) || (it.mail && await ToMail(it.f));

    public static List<Dictionary<string, string>> Queue() =>
        (CoreState.Get<List<Item>>(QKey) ?? new()).Select(i => i.f).ToList();

    public static int QueueCount => CoreState.Get<List<Item>>(QKey)?.Count ?? 0;

    static void SetQueue(List<Item> q)
    {
        if (q.Count > MaxQ) q = q.Skip(q.Count - MaxQ).ToList();
        CoreState.Set(QKey, q);
        try { QueueChanged?.Invoke(q.Count); } catch { }
    }

    /// <summary>→ "sent" | "queued"</summary>
    public static async Task<string> Post(string tur, string mesaj, string? iletisim = null, bool mail = false)
    {
        var it = new Item { f = Fields(tur, mesaj, iletisim), mail = mail };
        if (await Deliver(it)) { _ = FlushAsync(); return "sent"; }
        var q = CoreState.Get<List<Item>>(QKey) ?? new();
        q.Add(it);
        SetQueue(q);
        return "queued";
    }

    /// <summary>Kullanıcı mesajı (öneri / hata): gelen kutusu, olmazsa e-posta yedeği, o da olmazsa kuyruk.</summary>
    public static Task<string> Send(string tur, string mesaj, string? iletisim) => Post(tur == "hata" ? "hata" : "oneri", mesaj, iletisim, mail: true);

    public static async Task FlushAsync()
    {
        if (!await FlushGate.WaitAsync(0)) return;
        try
        {
            while (true)
            {
                var q = CoreState.Get<List<Item>>(QKey) ?? new();
                if (q.Count == 0) return;
                if (!await Deliver(q[0])) return;
                var rest = CoreState.Get<List<Item>>(QKey) ?? new();
                if (rest.Count > 0) rest.RemoveAt(0);
                SetQueue(rest);
            }
        }
        catch { }
        finally { FlushGate.Release(); }
    }
}
