using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using DDS.Core;

namespace DDS;

/// <param name="PatchUrl">FCD1 yaması (yalnızca değişen kısımlar); null = bu sürüm için yama yok.</param>
/// <param name="NeedsSetup">Kurulu sürüm latest.json → minVersion'ın altında: tam kurulum (win-setup) gerekir.</param>
public sealed record UpdateInfo(Version Version, string Url, long Size, string? PatchUrl = null, long PatchSize = 0,
    string? Sha256 = null, string? PatchSha256 = null, bool NeedsSetup = false, Dictionary<string, string>? Notes = null);

/// <summary>
/// Tek tıkla güncelleme — ortak çekirdek kuralları (SPEC.md): GitHub Releases → latest.json → kanal "win-exe"
/// (tek exe) ya da minVersion altındaysa "win-setup" (NSIS kurulumu). Önce kurulu sürüme ait FCD1 yaması denenir,
/// sonuç SHA-256 ile doğrulanır; yama yoksa ya da hata olursa kendiliğinden tam dosyaya geçilir.
/// </summary>
public static class Updater
{
    public const string Owner = "departures-mil";
    public const string Repo = "DesktopDuzenSabitleyici";
    public const string Channel = "win-exe";
    public const string SetupChannel = "win-setup";
    /// <summary>Eski istemcilerin (1.0.x) aradığı exe adı — yeni sürümlerde de aynı adla yayınlanır (köprü).</summary>
    public const string AssetName = "DesktopDuzenSabitleyici.exe";
    public static string ManifestUrl = $"https://github.com/{Owner}/{Repo}/releases/latest/download/latest.json";
    public static string ApiLatestUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    public static string DownloadBase = $"https://github.com/{Owner}/{Repo}/releases/download";

    public static Version Current
    {
        get
        {
            var v = typeof(Updater).Assembly.GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Current.Build}";

    static HttpClient NewClient()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        h.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DesktopDuzenSabitleyici", CurrentText));
        return h;
    }

    static Version? V(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || !Version.TryParse(s.Trim().TrimStart('v', 'V'), out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }

    /// <summary>latest.json → güncelleme planı (saf işlev; testler bunu doğrudan çağırır).</summary>
    public static UpdateInfo? Plan(string manifestJson, Version current, string? currentText = null)
    {
        var m = JsonNode.Parse(manifestJson) as JsonObject ?? throw new InvalidDataException("latest.json");
        var v = V((string?)m["version"]) ?? throw new InvalidDataException("latest.json: version");
        if (v <= current) return null;
        var min = V((string?)m["minVersion"]);
        bool setup = min != null && current < min;
        var assets = m["assets"] as JsonObject ?? throw new InvalidDataException("latest.json: assets");
        var a = assets[setup ? SetupChannel : Channel] as JsonObject;
        if (a == null && setup) { a = assets[Channel] as JsonObject; setup = false; }
        if (a == null) return null;
        string Url(string file) => $"{DownloadBase}/v{v.ToString(3)}/{Uri.EscapeDataString(file)}";
        var file = (string?)a["file"] ?? throw new InvalidDataException("latest.json: file");
        string? pUrl = null, pSha = null;
        long pSize = 0;
        if (!setup && a["patches"] is JsonObject ps && ps[currentText ?? current.ToString(3)] is JsonObject p && (string?)p["file"] is string pf)
        {
            pUrl = Url(pf);
            pSize = (long?)p["size"] ?? 0;
            pSha = (string?)p["sha256"];
        }
        Dictionary<string, string>? notes = null;
        if (m["notes"] is JsonObject n)
            notes = n.Where(x => x.Value is JsonValue).ToDictionary(x => x.Key, x => x.Value!.ToString());
        return new UpdateInfo(v, Url(file), (long?)a["size"] ?? 0, pUrl, pSize, (string?)a["sha256"], pSha, setup, notes);
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var http = NewClient();
        http.Timeout = TimeSpan.FromSeconds(20);
        using (var res = await http.GetAsync(ManifestUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60))
        {
            if (res.IsSuccessStatusCode) return Plan(await res.Content.ReadAsStringAsync(), Current, CurrentText);
            if ((int)res.StatusCode != 404) return null;
        }
        // latest.json'u olmayan (1.1.0 öncesi) bir release "latest" ise: eski yol, yalnızca tam exe.
        using var req = new HttpRequestMessage(HttpMethod.Get, ApiLatestUrl);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var r2 = await http.SendAsync(req);
        if (!r2.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await r2.Content.ReadAsStringAsync());
        var v = V(doc.RootElement.GetProperty("tag_name").GetString());
        if (v == null || v <= Current) return null;
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
            if (string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                return new UpdateInfo(v, a.GetProperty("browser_download_url").GetString()!, a.GetProperty("size").GetInt64());
        return null;
    }

    /// <summary>
    /// Yeni dosyayı getirir. Kurulu sürüme ait yama varsa yalnızca değişen kısımlar iner ve diskteki exe ile birleştirilir;
    /// sonuç SHA-256 ile doğrulanır. Yamada bir sorun olursa kendiliğinden tam dosyaya geçilir.
    /// progress(inen, toplam); patchMode(true) = yalnızca değişen kısımlar.
    /// </summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, Action<long, long> progress, CancellationToken ct,
        Action<bool>? patchMode = null, string? installedFile = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "DDS_update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, info.NeedsSetup ? $"DesktopDuzenSabitleyici-Setup-{info.Version.ToString(3)}.exe" : $"DesktopDuzenSabitleyici_{info.Version.ToString(3)}.exe");
        var cur = installedFile ?? Environment.ProcessPath;

        if (info.PatchUrl != null && !info.NeedsSetup && cur != null && File.Exists(cur))
        {
            var patch = Path.Combine(dir, $"patch_{info.Version.ToString(3)}.fcd1");
            try
            {
                patchMode?.Invoke(true);
                await FetchAsync(info.PatchUrl, info.PatchSize, patch, progress, ct);
                if (info.PatchSha256 != null && !string.Equals(Fcd1.Sha256Hex(patch), info.PatchSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("patch sha256");
                await Task.Run(() => Fcd1.Apply(cur, patch, file), ct);
                if (info.Sha256 != null && !string.Equals(Fcd1.Sha256Hex(file), info.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("sha256");
                return file;
            }
            catch (OperationCanceledException) { throw; }
            catch { try { File.Delete(file); } catch { } }   // aşağıda tam dosyaya geç
            finally { try { File.Delete(patch); } catch { } }
        }

        patchMode?.Invoke(false);
        await FetchAsync(info.Url, info.Size, file, progress, ct);
        if (info.Sha256 != null && !string.Equals(Fcd1.Sha256Hex(file), info.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(file); } catch { }
            throw new InvalidDataException(CoreStrings.T("fc.update.bad"));
        }
        return file;
    }

    static async Task FetchAsync(string url, long expected, string file, Action<long, long> progress, CancellationToken ct)
    {
        using var http = NewClient();
        using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        long total = expected > 0 ? expected : res.Content.Headers.ContentLength ?? 0;

        await using var src = await res.Content.ReadAsStreamAsync(ct);
        await using (var dst = File.Create(file))
        {
            var buf = new byte[81920];
            long done = 0;
            var tick = Stopwatch.StartNew();
            int read;
            progress(0, total);
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;
                if (tick.ElapsedMilliseconds >= 150) { tick.Restart(); progress(done, total); }
            }
            progress(done, total);
            if (total > 0 && done < total) throw new IOException("Download incomplete");
        }
    }

    /// <summary>Kurulum gerekiyorsa kurulum programını çalıştırır; değilse çalışan exe'yi indirileniyle değiştirip yeniden başlatır.</summary>
    public static void ApplyAndRestart(string newFile, UpdateInfo? info = null)
    {
        if (info?.Notes != null)
            CoreState.Set("whatsnew.pending", new Dictionary<string, object> { ["version"] = info.Version.ToString(3), ["notes"] = info.Notes });
        var cur = Environment.ProcessPath!;
        int pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), "DDS_update", "apply.cmd");
        if (info?.NeedsSetup == true)
        {
            // 1.1.2: kurulum SESSİZ (/S) ve gizli konsolda: program kapanınca kurulur, bitince program yeniden açılır.
            var setup = newFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? newFile : newFile + ".exe";
            if (setup != newFile) File.Move(newFile, setup, true);
            File.WriteAllText(script,
$@"@echo off
chcp 65001 >nul
:wait
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul && (ping -n 2 127.0.0.1 >nul & goto wait)
start """" /wait ""{setup}"" /S
del ""{setup}"" >nul 2>&1
if exist ""%LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici\{AssetName}"" (start """" ""%LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici\{AssetName}"" --updated) else if exist ""{cur}"" start """" ""{cur}"" --updated
del ""%~f0""
");
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            return;
        }
        File.WriteAllText(script,
$@"@echo off
chcp 65001 >nul
:wait
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul && (ping -n 2 127.0.0.1 >nul & goto wait)
set n=0
:copy
copy /y ""{newFile}"" ""{cur}"" >nul && goto ok
set /a n+=1
if %n% lss 20 (ping -n 2 127.0.0.1 >nul & goto copy)
:ok
del ""{newFile}"" >nul 2>&1
start """" ""{cur}"" --updated
del ""%~f0""
");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}
