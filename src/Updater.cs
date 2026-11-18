using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DDS;

public sealed record UpdateInfo(Version Version, string Url, long Size);

/// <summary>One-click updates from GitHub Releases.</summary>
public static class Updater
{
    public const string Owner = "departures-mil";
    public const string Repo = "DesktopDuzenSabitleyici";
    public const string AssetName = "DesktopDuzenSabitleyici.exe";

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

    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var http = NewClient();
        http.Timeout = TimeSpan.FromSeconds(20);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
        v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        if (v <= Current) return null;
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (!string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase)) continue;
            return new UpdateInfo(v, a.GetProperty("browser_download_url").GetString()!, a.GetProperty("size").GetInt64());
        }
        return null;
    }

    /// <summary>Downloads the new exe. progress(downloadedBytes, totalBytes, bytesPerSecond).</summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, Action<long, long, double> progress, CancellationToken ct)
    {
        var dir = Path.Combine(Path.GetTempPath(), "DDS_update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"DesktopDuzenSabitleyici_{info.Version}.exe");

        using var http = NewClient();
        using var res = await http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        long total = res.Content.Headers.ContentLength ?? info.Size;

        await using var src = await res.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(file);
        var buf = new byte[81920];
        long done = 0, windowBytes = 0;
        var sw = Stopwatch.StartNew();
        var window = Stopwatch.StartNew();
        double speed = 0;
        int read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, read), ct);
            done += read;
            windowBytes += read;
            if (window.ElapsedMilliseconds >= 400)
            {
                double inst = windowBytes / window.Elapsed.TotalSeconds;
                speed = speed == 0 ? inst : speed * 0.6 + inst * 0.4;
                windowBytes = 0;
                window.Restart();
                progress(done, total, speed);
            }
        }
        if (speed == 0) speed = done / Math.Max(0.001, sw.Elapsed.TotalSeconds);
        progress(done, total, speed);
        if (total > 0 && done < total) throw new IOException("Download incomplete");
        return file;
    }

    /// <summary>Replaces the running exe with the downloaded one and restarts.</summary>
    public static void ApplyAndRestart(string newFile)
    {
        var cur = Environment.ProcessPath!;
        int pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), "DDS_update", "apply.cmd");
        File.WriteAllText(script,
$@"@echo off
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
