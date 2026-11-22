using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DDS;

/// <summary>
/// Admin duyuruları (https://github.com/departures-mil/duyurular).
/// Açılışta ve 30 dakikada bir okunur; yeni duyuru bir kez pencere olarak açılır, 🔔 düğmesi listeyi gösterir.
/// </summary>
public sealed class Announcements
{
    const string Source = "https://raw.githubusercontent.com/departures-mil/duyurular/main/duyurular.json";
    const string AppId = "desktop-duzen";
    static readonly string SeenPath = Path.Combine(Storage.Dir, "duyurular-gorulen.json");
    static readonly string CachePath = Path.Combine(Storage.Dir, "duyurular-onbellek.json");

    static readonly Dictionary<string, Dictionary<string, string>> Txt = new()
    {
        ["tr"] = new() { ["title"] = "Duyurular", ["ok"] = "Tamam", ["close"] = "Kapat", ["none"] = "Henüz duyuru yok.", ["link"] = "Bağlantıyı aç", ["imp"] = "Önemli", ["upd"] = "Şimdi güncelle" },
        ["en"] = new() { ["title"] = "Announcements", ["ok"] = "OK", ["close"] = "Close", ["none"] = "No announcements yet.", ["link"] = "Open link", ["imp"] = "Important", ["upd"] = "Update now" },
        ["ar"] = new() { ["title"] = "الإعلانات", ["ok"] = "حسناً", ["close"] = "إغلاق", ["none"] = "لا توجد إعلانات بعد.", ["link"] = "فتح الرابط", ["imp"] = "مهم", ["upd"] = "حدّث الآن" },
    };

    readonly Form owner;
    readonly Button bell;
    readonly Action? update;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 30 * 60 * 1000 };
    List<JsonObject> list = new();
    List<string> seen = new();
    bool showing;

    public Announcements(Form owner, Button bell, Action? update)
    {
        this.owner = owner; this.bell = bell; this.update = update;
        bell.Visible = false;
        bell.Click += (_, _) => ShowList();
        try { if (File.Exists(SeenPath)) seen = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(SeenPath)) ?? new(); } catch { }
        try { if (File.Exists(CachePath)) Apply(JsonNode.Parse(File.ReadAllText(CachePath)) as JsonArray, false); } catch { }
        timer.Tick += async (_, _) => await RefreshAsync();
    }

    public async Task StartAsync()
    {
        timer.Start();
        await RefreshAsync();
    }

    static string L => Txt.ContainsKey(Lang.Current) ? Lang.Current : "tr";
    static string T(string k) => Txt[L][k];

    static string Pick(JsonNode? n)
    {
        if (n is null) return "";
        if (n is JsonValue v) return v.ToString();
        if (n is JsonObject o)
            foreach (var k in new[] { L, "tr", "en", "ar" })
                if (o[k] is JsonNode x && x.ToString().Length > 0) return x.ToString();
        return "";
    }

    async Task RefreshAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopDuzenSabitleyici");
            var json = await http.GetStringAsync($"{Source}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60}");
            var arr = JsonNode.Parse(json)?["duyurular"] as JsonArray;
            try { Directory.CreateDirectory(Storage.Dir); File.WriteAllText(CachePath, arr?.ToJsonString() ?? "[]"); } catch { }
            Apply(arr, true);
        }
        catch { }
    }

    static int Cmp(string a, string b)
    {
        int[] P(string v) => v.TrimStart('v', 'V', '.').Split('.').Select(x => int.TryParse(x, out var i) ? i : 0).ToArray();
        var x = P(a); var y = P(b);
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int p = i < x.Length ? x[i] : 0, q = i < y.Length ? y[i] : 0;
            if (p != q) return p < q ? -1 : 1;
        }
        return 0;
    }

    static bool Fits(JsonObject d)
    {
        if (d["id"] is null || d["aktif"]?.GetValueKind() == JsonValueKind.False) return false;
        var h = (d["hedef"] as JsonArray)?.Select(x => x?.ToString() ?? "").ToList() ?? new List<string> { "*" };
        if (!h.Contains("*") && !h.Contains(AppId)) return false;
        var now = DateTime.Now;
        if (DateTime.TryParse(d["baslangic"]?.ToString(), out var b) && b > now) return false;
        if (DateTime.TryParse(d["bitis"]?.ToString(), out var e) && e < now) return false;
        if (d["altSurum"] is JsonNode lo && Cmp(Updater.CurrentText, lo.ToString()) < 0) return false;
        if (d["ustSurum"] is JsonNode hi && Cmp(Updater.CurrentText, hi.ToString()) > 0) return false;
        return true;
    }

    void Apply(JsonArray? arr, bool show)
    {
        list = (arr ?? new JsonArray()).OfType<JsonObject>().Where(Fits)
            .OrderByDescending(d => d["tarih"]?.ToString() ?? "").ToList();
        UpdateBell();
        if (show) ShowNext();
    }

    void MarkSeen(string id)
    {
        if (seen.Contains(id)) return;
        seen.Add(id);
        if (seen.Count > 300) seen = seen.Skip(seen.Count - 300).ToList();
        try { Directory.CreateDirectory(Storage.Dir); File.WriteAllText(SeenPath, JsonSerializer.Serialize(seen)); } catch { }
    }

    void UpdateBell()
    {
        int n = list.Count(d => !seen.Contains(d["id"]!.ToString()));
        bell.Text = n > 0 ? $"🔔{n}" : "🔔";
        bell.ForeColor = n > 0 ? Color.FromArgb(255, 210, 74) : Theme.Text;
        bell.Visible = list.Count > 0;
    }

    void ShowNext()
    {
        if (showing) return;
        var d = list.AsEnumerable().Reverse().FirstOrDefault(x => !seen.Contains(x["id"]!.ToString()));
        if (d is null) return;
        showing = true;
        MarkSeen(d["id"]!.ToString());
        try { Dialog(new[] { d }, d["guncellemeDugmesi"]?.GetValueKind() == JsonValueKind.True); }
        finally { showing = false; UpdateBell(); }
        ShowNext();
    }

    void ShowList()
    {
        foreach (var d in list) MarkSeen(d["id"]!.ToString());
        UpdateBell();
        Dialog(list, false);
    }

    void Dialog(IList<JsonObject> items, bool withUpdate)
    {
        bool rtl = L == "ar";
        using var f = new Form
        {
            Text = "🔔 " + T("title"), StartPosition = FormStartPosition.CenterScreen, TopMost = true,
            Width = 560, Height = 440, BackColor = Theme.Card, ForeColor = Theme.Text, MinimizeBox = false, MaximizeBox = false,
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No, RightToLeftLayout = rtl, ShowIcon = false,
        };
        var box = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 10.5f), DetectUrls = true, RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
        };
        box.LinkClicked += (_, e) => { try { Process.Start(new ProcessStartInfo(e.LinkText!) { UseShellExecute = true }); } catch { } };
        if (items.Count == 0) box.AppendText(T("none"));
        foreach (var d in items)
        {
            box.SelectionFont = new Font("Segoe UI", 12.5f, FontStyle.Bold);
            box.AppendText((d["onem"]?.ToString() == "onemli" ? "[" + T("imp") + "] " : "") + Pick(d["baslik"]) + "\n");
            box.SelectionFont = new Font("Segoe UI", 9f);
            box.SelectionColor = Color.Gray;
            box.AppendText((DateTime.TryParse(d["tarih"]?.ToString(), out var t) ? t.ToString("dd.MM.yyyy") : "") + "\n");
            box.SelectionColor = Theme.Text;
            box.SelectionFont = new Font("Segoe UI", 10.5f);
            box.AppendText(Pick(d["metin"]) + "\n");
            if (d["link"] is JsonNode lk) box.AppendText(lk + "\n");
            box.AppendText("\n");
        }
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = rtl ? FlowDirection.LeftToRight : FlowDirection.RightToLeft, Padding = new Padding(8), BackColor = Theme.Card };
        var ok = new Button { Text = items.Count > 1 ? T("close") : T("ok"), AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text };
        ok.Click += (_, _) => f.Close();
        bar.Controls.Add(ok);
        if (withUpdate && update != null)
        {
            var up = new Button { Text = T("upd"), AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text };
            up.Click += (_, _) => { f.Close(); update(); };
            bar.Controls.Add(up);
        }
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Theme.Card };
        pad.Controls.Add(box);
        f.Controls.Add(pad);
        f.Controls.Add(bar);
        f.AcceptButton = ok;
        f.ShowDialog(owner.Visible ? owner : null);
    }
}
