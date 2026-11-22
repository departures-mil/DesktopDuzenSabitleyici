using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DDS;

/// <summary>Backs up and syncs layouts to a private (secret) GitHub gist on the user's account.</summary>
public static class GitHubSync
{
    const string Description = "DesktopDuzenSabitleyici-layouts";
    const string FileName = "dds-layouts.json";
    public const string TokenUrl =
        "https://github.com/settings/tokens/new?scopes=gist&description=Desktop%20Duzen%20Sabitleyici";

    public static string? GetToken(Settings s)
    {
        if (string.IsNullOrEmpty(s.TokenEnc)) return null;
        try
        {
            var raw = Dpapi.Unprotect(Convert.FromBase64String(s.TokenEnc));
            return Encoding.UTF8.GetString(raw);
        }
        catch { return null; }
    }

    public static void SetToken(Settings s, string token)
    {
        var enc = Dpapi.Protect(Encoding.UTF8.GetBytes(token.Trim()));
        s.TokenEnc = Convert.ToBase64String(enc);
        s.GistId = null;
    }

    static HttpClient Client(string token)
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        h.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DesktopDuzenSabitleyici", Updater.CurrentText));
        h.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        h.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return h;
    }

    /// <summary>Merges local and remote histories (nothing is lost, newest first) and uploads the result.</summary>
    public static async Task<StoreData> SyncAsync(Settings s, StoreData local)
    {
        var token = GetToken(s) ?? throw new InvalidOperationException("token");
        using var http = Client(token);

        string? gistId = s.GistId;
        StoreData? remote = null;

        if (string.IsNullOrEmpty(gistId))
        {
            var res = await http.GetAsync("https://api.github.com/gists?per_page=100");
            await Ensure(res);
            var arr = JsonNode.Parse(await res.Content.ReadAsStringAsync())!.AsArray();
            foreach (var g in arr)
                if ((string?)g?["description"] == Description) { gistId = (string?)g!["id"]; break; }
        }

        if (!string.IsNullOrEmpty(gistId))
        {
            var res = await http.GetAsync($"https://api.github.com/gists/{gistId}");
            Capture(res);
            if (res.IsSuccessStatusCode)
            {
                var g = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
                var f = g["files"]?[FileName];
                if (f != null)
                {
                    string? content = (string?)f["content"];
                    if ((bool?)f["truncated"] == true && f["raw_url"] != null)
                        content = await http.GetStringAsync((string)f["raw_url"]!);
                    if (content != null) remote = Storage.Deserialize(content);
                }
            }
            else gistId = null;
        }

        var merged = Merge(local, remote);
        var payload = new JsonObject
        {
            ["description"] = Description,
            ["files"] = new JsonObject { [FileName] = new JsonObject { ["content"] = Storage.Serialize(merged) } },
        };

        HttpResponseMessage up;
        if (string.IsNullOrEmpty(gistId))
        {
            payload["public"] = false;
            up = await http.PostAsync("https://api.github.com/gists", Json(payload));
        }
        else
        {
            up = await http.PatchAsync($"https://api.github.com/gists/{gistId}", Json(payload));
        }
        await Ensure(up);
        s.GistId = (string?)JsonNode.Parse(await up.Content.ReadAsStringAsync())!["id"];
        return merged;
    }

    static StringContent Json(JsonNode n) => new(n.ToJsonString(), Encoding.UTF8, "application/json");

    // v1.0.6: Ayarlar > Kullanım Limitleri — GitHub'ın saatlik istek sınırı (kullanıcının kendi anahtarı)
    public static int? RateRemaining { get; private set; }
    public static int? RateLimit { get; private set; }
    public static DateTime? RateReset { get; private set; }
    public static DateTime? LastSync { get; private set; }

    static void Capture(HttpResponseMessage r)
    {
        try
        {
            if (r.Headers.TryGetValues("X-RateLimit-Remaining", out var rem) && int.TryParse(rem.FirstOrDefault(), out var a)) RateRemaining = a;
            if (r.Headers.TryGetValues("X-RateLimit-Limit", out var lim) && int.TryParse(lim.FirstOrDefault(), out var b)) RateLimit = b;
            if (r.Headers.TryGetValues("X-RateLimit-Reset", out var rs) && long.TryParse(rs.FirstOrDefault(), out var c))
                RateReset = DateTimeOffset.FromUnixTimeSeconds(c).LocalDateTime;
            LastSync = DateTime.Now;
        }
        catch { }
    }

    static async Task Ensure(HttpResponseMessage r)
    {
        Capture(r);
        if (r.IsSuccessStatusCode) return;
        string body = "";
        try { body = await r.Content.ReadAsStringAsync(); } catch { }
        string msg = (int)r.StatusCode == 401 ? "token" : $"{(int)r.StatusCode} {r.ReasonPhrase}";
        try { msg += " " + (string?)JsonNode.Parse(body)?["message"]; } catch { }
        throw new HttpRequestException(msg.Trim());
    }

    static StoreData Merge(StoreData local, StoreData? remote)
    {
        if (remote == null) return local;
        var m = new StoreData { LastKey = local.LastKey };
        foreach (var src in new[] { local, remote })
            foreach (var p in src.Displays.Values)
            {
                if (!m.Displays.TryGetValue(p.Key, out var t))
                {
                    t = new Profile { Key = p.Key, Display = p.Display, Monitors = p.Monitors };
                    m.Displays[p.Key] = t;
                }
                foreach (var h in p.History)
                    if (t.History.All(x => x.Id != h.Id)) t.History.Add(h);
            }
        foreach (var p in m.Displays.Values) StoreData.Trim(p);
        return m;
    }
}

/// <summary>Windows DPAPI: the key is encrypted so only this Windows user can read it.</summary>
static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    struct BLOB { public int cbData; public IntPtr pbData; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref BLOB pDataIn, string? desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref BLOB pDataOut);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref BLOB pDataIn, IntPtr desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref BLOB pDataOut);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr h);

    public static byte[] Protect(byte[] data) => Run(data, true);
    public static byte[] Unprotect(byte[] data) => Run(data, false);

    static byte[] Run(byte[] data, bool protect)
    {
        var input = new BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, data.Length)) };
        var output = new BLOB();
        try
        {
            Marshal.Copy(data, 0, input.pbData, data.Length);
            bool ok = protect
                ? CryptProtectData(ref input, "DDS", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref output);
            if (!ok) throw new InvalidOperationException("DPAPI");
            var r = new byte[output.cbData];
            Marshal.Copy(output.pbData, r, 0, output.cbData);
            return r;
        }
        finally
        {
            Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }
}
