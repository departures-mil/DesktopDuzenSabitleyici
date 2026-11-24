using System.Globalization;

namespace DDS.Core;

/// <summary>Çekirdek metinleri (fc.*, tr/en/ar — Generated.cs) ve sayı biçimi (web FC.i18n.num karşılığı).</summary>
public static partial class CoreStrings
{
    /// <summary>Etkin dil (uygulama Lang.Current'ı bağlar).</summary>
    public static Func<string> Lang { get; set; } = () => "tr";

    static int Idx(string? l) => (l ?? Lang()) switch { "en" => 1, "ar" => 2, _ => 0 };

    public static string T(string key, string? lang = null) =>
        S.TryGetValue(key, out var v) ? v[Idx(lang)] : key;

    /// <summary>{n}, {v}, {e} gibi adlı yer tutucular.</summary>
    public static string T(string key, IDictionary<string, object?> vars, string? lang = null)
    {
        var s = T(key, lang);
        foreach (var kv in vars) s = s.Replace("{" + kv.Key + "}", Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
        return s;
    }

    public static string T1(string key, string name, object? value, string? lang = null) =>
        T(key, new Dictionary<string, object?> { [name] = value }, lang);

    static readonly NumberFormatInfo Tr = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NumberGroupSizes = new[] { 3 } };
    static readonly NumberFormatInfo En = new() { NumberDecimalSeparator = ".", NumberGroupSeparator = ",", NumberGroupSizes = new[] { 3 } };

    /// <summary>toLocaleString(dil, {min=max=digits}) ile aynı.</summary>
    public static string Num(double n, int digits, string? lang = null)
    {
        var nf = Idx(lang) == 0 ? Tr : En;
        var r = Math.Round((decimal)n, digits, MidpointRounding.AwayFromZero);
        return r.ToString("N" + digits, nf);
    }
}

/// <summary>İndirme ilerlemesi (web FC.progress): yüzde · MB / MB · kalan · hız; yama kipinde "yalnızca değişen kısımlar".</summary>
public sealed class ProgressInfo
{
    public double Done, Total, Pct, Remaining, Speed;
    public bool Finished;

    const double MB = 1048576.0;

    static string Mb(double b, string? l) => CoreStrings.Num(b / MB, 1, l) + " MB";
    static string Pc(double p, string? l) { var n = (int)Math.Floor(p); return (l ?? CoreStrings.Lang()) == "tr" ? "%" + n : n + "%"; }

    public string Format(bool patch, string? lang = null)
    {
        var l = lang ?? CoreStrings.Lang();
        var mode = patch ? CoreStrings.T("fc.update.patchMode", l) : "";
        var parts = new List<string>();
        if (Finished)
        {
            parts.Add(CoreStrings.T("fc.p.done", l));
            if (Total > 0) parts.Add(Mb(Total, l));
            if (mode.Length > 0) parts.Add(mode);
            return string.Join(" · ", parts);
        }
        if (Total > 0)
        {
            parts.Add(Pc(Pct, l));
            parts.Add(Mb(Done, l) + " / " + Mb(Total, l));
            parts.Add(CoreStrings.T1("fc.p.left", "x", Mb(Remaining, l), l));
        }
        else parts.Add(Mb(Done, l));
        parts.Add(Speed > 0 ? CoreStrings.Num(Speed / MB, 1, l) + " " + CoreStrings.T("fc.p.speed", l) : CoreStrings.T("fc.p.calc", l));
        return string.Join(" · ", parts);
    }
}

/// <summary>Hız: son ~3 saniyedeki örnekler (kayan pencere), web tracker ile aynı.</summary>
public sealed class ProgressTracker
{
    readonly List<(double t, double b)> samples = new();
    readonly Func<double> now;
    double total;

    public ProgressTracker(double total, Func<double>? nowMs = null)
    {
        this.total = total;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        now = nowMs ?? (() => sw.Elapsed.TotalMilliseconds);
        samples.Add((now(), 0));
    }

    public void SetTotal(double n) => total = n;

    public ProgressInfo Update(double done, bool finished = false)
    {
        done = Math.Max(0, done);
        var t = now();
        samples.Add((t, done));
        while (samples.Count > 2 && t - samples[0].t > 3000) samples.RemoveAt(0);
        var a = samples[0];
        var sec = (t - a.t) / 1000;
        var speed = sec >= 0.25 ? (done - a.b) / sec : 0;
        var tot = total > 0 && done <= total ? total : (finished ? done : 0);
        return new ProgressInfo
        {
            Done = done, Total = tot, Pct = tot > 0 ? Math.Min(100, done / tot * 100) : 0,
            Remaining = tot > 0 ? Math.Max(0, tot - done) : 0, Speed = Math.Max(0, speed), Finished = finished,
        };
    }
}
