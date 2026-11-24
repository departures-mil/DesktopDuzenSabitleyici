// Çekirdek testleri: FCD1 (fcdelta.js vektörleri), tokenlar/şemalar, metinler, ilerleme biçimi (web ile aynı),
// çökme maskesi (web ile aynı), eşitleme birleştirme kuralı (silme yok), latest.json planı,
// ve KÖPRÜ: 1.0.11'in güncelleyicisi (Old/OldUpdater.cs, git'ten birebir) yeni release düzenini görüp yeni sürümü alabiliyor mu.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DDS;
using DDS.Core;

int pass = 0, fail = 0;
void Ok(bool c, string name, string? info = null)
{
    if (c) { pass++; Console.WriteLine("  ✓ " + name); }
    else { fail++; Console.WriteLine("  ✗ " + name + (info != null ? "  → " + info : "")); }
}
async Task Group(string name, Func<Task> f)
{
    Console.WriteLine(name);
    try { await f(); } catch (Exception e) { fail++; Console.WriteLine("  ✗ beklenmeyen hata: " + e); }
}

string root = AppContext.BaseDirectory;
while (!Directory.Exists(Path.Combine(root, "tests", "fixtures"))) root = Path.GetDirectoryName(root) ?? throw new Exception("kök yok");
string fx = Path.Combine(root, "tests", "fixtures");
string tmp = Path.Combine(Path.GetTempPath(), "dds-tests-" + Environment.ProcessId);
Directory.CreateDirectory(tmp);
CoreState.FilePath = null; // testler diske yazmasın
byte[] Sha(byte[] b) => SHA256.HashData(b);

await Group("FCD1 (cekirdek/delta/node/fcdelta.js vektörleri)", async () =>
{
    foreach (var name in new[] { "empty-abc", "shift", "same", "to-empty" })
    {
        var old = Path.Combine(fx, "fcd1", name + ".old");
        var outF = Path.Combine(tmp, name + ".out");
        Fcd1.Apply(old, Path.Combine(fx, "fcd1", name + ".fcd1"), outF);
        Ok(File.ReadAllBytes(outF).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(fx, "fcd1", name + ".new"))), name);
    }
    // FORMAT.md §6 test vektörü: başlık
    var hb = File.ReadAllBytes(Path.Combine(fx, "fcd1", "empty-abc.fcd1"));
    Ok(Encoding.ASCII.GetString(hb, 0, 4) == "FCD1" && hb[4] == 1 && BitConverter.ToUInt64(hb, 45) == 3, "başlık: magic, codec, dstLen=3");
    Ok(Convert.ToHexString(hb, 53, 32).ToLowerInvariant().StartsWith("ba7816bf"), "başlık: dstSha256 = SHA-256(\"abc\")");
    // yanlış kaynak reddedilir ve çıktı dosyası oluşmaz
    var bad = Path.Combine(tmp, "bad.out");
    bool threw = false;
    try { Fcd1.Apply(Path.Combine(fx, "fcd1", "to-empty.old"), Path.Combine(fx, "fcd1", "shift.fcd1"), bad); } catch (InvalidDataException) { threw = true; }
    Ok(threw && !File.Exists(bad), "yanlış kaynak → hata, çıktı yok");
    // bozuk gövde
    var corrupt = Path.Combine(tmp, "corrupt.fcd1");
    var cb = File.ReadAllBytes(Path.Combine(fx, "fcd1", "shift.fcd1"));
    cb[cb.Length / 2] ^= 0x5A;
    File.WriteAllBytes(corrupt, cb);
    threw = false;
    try { Fcd1.Apply(Path.Combine(fx, "fcd1", "shift.old"), corrupt, bad); } catch (Exception) { threw = true; }
    Ok(threw && !File.Exists(bad), "bozuk yama → hata, çıktı yok");
    // bilinmeyen codec
    var cod = (byte[])hb.Clone(); cod[4] = 2;
    File.WriteAllBytes(corrupt, cod);
    threw = false;
    try { Fcd1.Apply(Path.Combine(fx, "fcd1", "empty-abc.old"), corrupt, bad); } catch (InvalidDataException) { threw = true; }
    Ok(threw, "codec 0x02 reddedilir");
    await Task.CompletedTask;
});

await Group("Görünüm: şemalar ve tokenlar", async () =>
{
    Ok(CoreTokens.Order.Length == 8 && CoreTokens.Order[0] == "falcon" && CoreTokens.Order.Contains("klasik"), "8 şema, falcon ilk, klasik var");
    foreach (var id in CoreTokens.Order)
        foreach (var mode in new[] { "dark", "light" })
        {
            Look.Set(id, mode, 1);
            var miss = CoreTokens.Names.Where(n => !CoreTokens.Schemes[id][mode == "dark" ? 0 : 1].ContainsKey(n)).ToList();
            Ok(miss.Count == 0, $"{id}/{mode}: tüm tokenlar", string.Join(",", miss));
            var bg = Look.Tok("bg"); var panel = Look.Tok("panel-solid");
            Ok(Look.Contrast(Look.Tok("text"), panel) >= 4.5, $"{id}/{mode}: metin/panel kontrastı ≥ 4.5", Look.Contrast(Look.Tok("text"), panel).ToString("F2"));
            Ok(Look.Contrast(Look.Tok("muted"), panel) >= 3.0, $"{id}/{mode}: soluk metin/panel ≥ 3", Look.Contrast(Look.Tok("muted"), panel).ToString("F2"));
            Ok(bg.A == 255 && Look.Tok("topbar-bg").A == 255, $"{id}/{mode}: saydam tonlar opak karıştırılıyor");
        }
    Look.Set("klasik", "dark", 1);
    Ok(Look.Tok("bg") == Color.FromArgb(15, 23, 42) && Look.Tok("panel-solid") == Color.FromArgb(30, 41, 59) && Look.Tok("accent") == Color.FromArgb(59, 130, 246)
       && Look.Tok("topbar-bg") == Color.FromArgb(10, 16, 32), "klasik koyu = 1.0.x renkleri (bg, kart, vurgu, başlık)");
    Look.Set("falcon", "dark", 1);
    Ok(Look.Raw("accent") == "#22d3ee" && Look.Raw("bg") == "#070b14", "falcon koyu = Falcon Ofis değerleri");
    Ok(Look.Parse("#0000").A == 0 && Look.Parse("#22d3ee29").A == 0x29 && Look.Parse("#fff") == Color.FromArgb(255, 255, 255, 255), "hex ayrıştırma (#rgba, #rrggbbaa, #rgb)");
    Look.Set("yok-boyle", "garip", 9);
    Ok(Look.Scheme == "falcon" && Look.Mode == "dark" && Look.Scale == 1.4, "bilinmeyen şema/mod → varsayılan; yazı boyutu 0.85–1.4");
    Look.Set(null, "auto", 0.1);
    Look.SystemLight = () => true;
    Ok(Look.Effective == "light" && Look.Scale == 0.85, "otomatik mod sistem açık temasını izler");
    Look.SystemLight = () => false;
    Look.Set("falcon", "dark", 1);
    await Task.CompletedTask;
});

await Group("Metinler ve ilerleme (web FC.progress ile aynı)", async () =>
{
    Ok(CoreStrings.T("fc.contact.title", "tr") == "Admin ile iletişim" && CoreStrings.T("fc.update.patchMode", "en") == "changed parts only"
       && CoreStrings.T("fc.update.patchMode", "ar") == "الأجزاء المتغيرة فقط", "fc.* metinleri tr/en/ar");
    Ok(CoreStrings.T("fc.scheme.klasik", "tr").StartsWith("Klasik"), "klasik şema etiketi");
    var arr = JsonNode.Parse(File.ReadAllText(Path.Combine(fx, "progress.json")))!.AsArray();
    int bad = 0;
    foreach (var c in arr)
    {
        var p = c!["p"]!;
        var pi = new ProgressInfo { Done = (double)p["done"]!, Total = (double)p["total"]!, Pct = (double)p["pct"]!, Remaining = (double)p["remaining"]!, Speed = (double)p["speed"]!, Finished = (bool)p["finished"]! };
        var got = pi.Format((bool)c["patch"]!, (string)c["lang"]!);
        if (got != (string)c["out"]!) { bad++; Console.WriteLine($"    {(string)c["lang"]!}: '{got}' ≠ '{(string)c["out"]!}'"); }
    }
    Ok(bad == 0, $"{arr.Count} ilerleme satırı web ile birebir");
    double t = 0;
    var tr = new ProgressTracker(10 * 1048576, () => t);
    t = 1000; tr.Update(1048576);
    t = 2000; var pp = tr.Update(2 * 1048576);
    Ok(Math.Abs(pp.Speed - 1048576) < 1 && pp.Pct == 20 && pp.Remaining == 8 * 1048576, "hız (kayan pencere), yüzde, kalan");
    await Task.CompletedTask;
});

await Group("Çökme raporu", async () =>
{
    var arr = JsonNode.Parse(File.ReadAllText(Path.Combine(fx, "crash.json")))!.AsArray();
    foreach (var c in arr)
    {
        var got = Crash.Sanitize((string)c!["in"]!);
        Ok(got == (string)c["out"]!, "maske = web: " + (string)c["out"]!, got);
    }
    Ok(Crash.Hash("abc") == "b885c8b" || Crash.Hash("abc") == Convert.ToString(unchecked((uint)((((5381u * 33) + 'a') * 33 + 'b') * 33 + 'c')), 16), "djb2");
    CoreState.Reset();
    var sent = new List<string>();
    Crash.Sender = s => { sent.Add(s); return Task.CompletedTask; };
    Crash.Version = () => "1.1.0";
    var day = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);
    Crash.Now = () => day;
    Exception Mk(string m) { try { throw new InvalidOperationException(m); } catch (Exception e) { return e; } }
    var e1 = Mk("aynı hata");
    Ok(Crash.Report(e1, "ui") && !Crash.Report(Mk("aynı hata"), "ui"), "aynı imza bir kez");
    for (int i = 0; i < 10; i++) Crash.Report(Mk("hata " + new string('x', i + 1)), "ui");
    Ok(sent.Count == 5 && Crash.SentToday() == 5, "günde en çok 5 rapor", sent.Count.ToString());
    day = day.AddDays(1);
    Ok(Crash.Report(Mk("ertesi gün"), "ui") && sent.Count == 6, "ertesi gün sayaç sıfırlanır");
    Ok(sent[0].StartsWith("[ui] InvalidOperationException: aynı hata") && sent[0].Contains("\n#"), "rapor biçimi: [kaynak] tür: ileti … #imza");
    Ok(!Crash.Report(new OperationCanceledException(), "ui"), "iptal hataları yok sayılır");
    Crash.Enabled = false;
    Ok(!Crash.Report(Mk("kapalıyken"), "ui"), "kapalıyken gönderilmez");
    Crash.Enabled = true;
    await Task.CompletedTask;
});

await Group("Admin gelen kutusu (SPEC alanları, kuyruk)", async () =>
{
    CoreState.Reset();
    var posts = new List<(string url, string body)>();
    bool online = false;
    Inbox.Version = () => "1.1.0";
    Inbox.PostOverride = (u, b, h) => { posts.Add((u, b)); return Task.FromResult(online ? (200, u.Contains("formsubmit") ? "{\"success\":\"true\"}" : "{}") : (0, "")); };
    var r = await Inbox.Send("hata", "deneme", "a@b.co");
    Ok(r == "queued" && Inbox.QueueCount == 1, "çevrimdışı → kuyruk");
    var doc = JsonNode.Parse(posts[0].body)!["fields"]!;
    var keys = doc.AsObject().Select(x => x.Key).OrderBy(x => x).ToArray();
    Ok(string.Join(",", keys) == "cihaz,dil,iletisim,mesaj,platform,surum,tur,uygulama,zaman", "alanlar", string.Join(",", keys));
    Ok((string?)doc["tur"]!["stringValue"] == "hata" && (string?)doc["uygulama"]!["stringValue"] == "desktop-duzen-sabitleyici" && posts[0].url.Contains("comm-calc-103111111") && posts[0].url.Contains("/mesajlar"), "tur, uygulama, adres");
    online = true;
    await Inbox.FlushAsync();
    Ok(Inbox.QueueCount == 0, "bağlantı gelince kuyruk boşalır");
    Inbox.PostOverride = null;
});

await Group("Eşitleme birleştirme kuralı (silme yok)", async () =>
{
    Snapshot S(string id, int x, string src = "user", int day = 1) => new() { Id = id, SavedUtc = new DateTime(2026, 11, day, 0, 0, 0, DateTimeKind.Utc), Source = src, Icons = { new IconEntry { Key = "a", X = x, Y = 0 } } };
    StoreData Mk(params Snapshot[] s) { var d = new StoreData(); d.Displays["K"] = new Profile { Key = "K", Display = "Ekran", History = s.ToList() }; return d; }
    var local = Mk(S("1", 10), S("2", 20));
    var remote = Mk(S("2", 20), S("3", 30));
    var m = SyncMerge.Merge(local, remote);
    Ok(m.Displays["K"].History.Select(h => h.Id).OrderBy(x => x).SequenceEqual(new[] { "1", "2", "3" }), "birleşim: hiçbir kayıt kaybolmaz");
    // uzakta silinen (arşive taşınan) kayıt yerelde de arşive gider — silinmez
    remote.ArchiveSnapshot("2");
    m = SyncMerge.Merge(local, remote);
    Ok(!m.Displays["K"].History.Any(h => h.Id == "2") && m.Archive.Any(a => a.Snap.Id == "2" && a.Reason == "deleted"), "uzak silme → arşiv");
    // eski (1.0.x) istemci kaydı gerçekten sildi: birleşim geri getirir
    var oldClient = Mk(S("3", 30));
    m = SyncMerge.Merge(Mk(S("1", 10), S("3", 30)), oldClient);
    Ok(m.Displays["K"].History.Any(h => h.Id == "1"), "1.0.x'in sildiği kayıt yerelden geri gelir");
    // çakışma: aynı kimlik, farklı simgeler → yerel kalır, öbürü arşive "conflict"
    m = SyncMerge.Merge(Mk(S("9", 10, "manual")), Mk(S("9", 99, "auto")));
    Ok(m.Displays["K"].History.Single(h => h.Id == "9").Icons[0].X == 10 && m.Archive.Any(a => a.Reason == "conflict" && a.Snap.Icons[0].X == 99), "çakışma → güçlü/yerel kalır, eski hal arşive");
    m = SyncMerge.Merge(Mk(S("8", 10, "auto")), Mk(S("8", 10, "manual")));
    Ok(m.Displays["K"].History.Single().Source == "manual" && m.Archive.Count == 0, "yalnız kaynak farkı: güçlü kaynak alınır, arşiv kirlenmez");
    // geçmiş sınırı: taşan arşive
    var many = Mk(Enumerable.Range(1, 30).Select(i => S("h" + i, i, "user", i % 28 + 1)).ToArray());
    m = SyncMerge.Merge(many, new StoreData());
    Ok(m.Displays["K"].History.Count == StoreData.MaxHistory && m.Archive.Count(a => a.Reason == "old") == 5, "sınırı aşan 5 kayıt arşive");
    // arşivden geri alma: yeni kimlik, arşiv kaydı kalır (Restored)
    var st = Mk(S("r", 5));
    st.ArchiveSnapshot("r");
    var back = st.RestoreFromArchive("r");
    Ok(back != null && back.Id != "r" && st.Displays["K"].History.Count == 1 && st.Archive.Single().Restored, "arşivden geri al");
    var json = Storage.Serialize(st);
    var again = Storage.Deserialize(json)!;
    Ok(again.Archive.Count == 1 && again.Archive[0].Restored, "arşiv JSON'da korunur");
    await Task.CompletedTask;
});

await Group("latest.json planı", async () =>
{
    string Man(string ver, string min = "1.0.0") => JsonSerializer.Serialize(new
    {
        schema = 1, app = "desktop-duzen-sabitleyici", version = ver, minVersion = min, notes = new { tr = "notlar", en = "notes", ar = "ملاحظات" },
        assets = new Dictionary<string, object>
        {
            ["win-exe"] = new { file = "DesktopDuzenSabitleyici.exe", size = 100, sha256 = "aa", patches = new Dictionary<string, object> { ["1.0.11"] = new { file = "DesktopDuzenSabitleyici-win-exe.patch-from-1.0.11.bin", size = 5, sha256 = "bb" } } },
            ["win-setup"] = new { file = "DesktopDuzenSabitleyici-Setup-" + ver + ".exe", size = 200, sha256 = "cc", patches = new { } },
        },
    });
    var p = Updater.Plan(Man("1.1.0"), new Version(1, 0, 11))!;
    Ok(p.Version == new Version(1, 1, 0) && p.Url.EndsWith("/v1.1.0/DesktopDuzenSabitleyici.exe") && p.PatchUrl!.EndsWith("patch-from-1.0.11.bin") && p.Sha256 == "aa" && !p.NeedsSetup, "win-exe + kurulu sürümün yaması");
    Ok(Updater.Plan(Man("1.1.0"), new Version(1, 0, 9))!.PatchUrl == null, "yama yoksa tam dosya");
    Ok(Updater.Plan(Man("1.1.0"), new Version(1, 1, 0)) == null && Updater.Plan(Man("1.1.0"), new Version(1, 2, 0)) == null, "aynı/yeni sürümde güncelleme yok");
    var s = Updater.Plan(Man("1.3.0", "1.2.0"), new Version(1, 1, 0))!;
    Ok(s.NeedsSetup && s.Url.EndsWith("Setup-1.3.0.exe") && s.PatchUrl == null, "minVersion altı → win-setup");
    Ok(p.Notes!["tr"] == "notlar", "sürüm notları");
    await Task.CompletedTask;
});

await Group("KÖPRÜ: 1.0.11 güncelleyicisi + yeni istemci, yerel sahte GitHub ile uçtan uca", async () =>
{
    // eski exe = A, yeni exe = B (gerçek boyutta değil ama kayan veri içeriyor)
    var A = File.ReadAllBytes(Path.Combine(fx, "fcd1", "shift.old"));
    var B = File.ReadAllBytes(Path.Combine(fx, "fcd1", "shift.new"));
    var fcd = File.ReadAllBytes(Path.Combine(fx, "fcd1", "shift.fcd1"));
    var legacy = DDS.Delta.Create(A, B); // CI legacy-patches işi bunu üretir (tools/DeltaMaker)
    string H(byte[] b) => Convert.ToHexString(Sha(b)).ToLowerInvariant();
    var files = new Dictionary<string, byte[]>
    {
        ["DesktopDuzenSabitleyici.exe"] = B,
        ["DesktopDuzenSabitleyici-Setup-1.1.0.exe"] = Encoding.ASCII.GetBytes("MZ setup"),
        ["DesktopDuzenSabitleyici-win-exe.patch-from-1.0.11.bin"] = fcd,
        ["DesktopDuzenSabitleyici-patch-from-1.0.11.bin"] = legacy,
    };
    var manifest = JsonSerializer.Serialize(new
    {
        schema = 1, app = "desktop-duzen-sabitleyici", version = "1.1.0", minVersion = "1.0.0", notes = new { tr = "n", en = "n", ar = "n" },
        assets = new Dictionary<string, object>
        {
            ["win-exe"] = new { file = "DesktopDuzenSabitleyici.exe", size = B.Length, sha256 = H(B), patches = new Dictionary<string, object> { ["1.0.11"] = new { file = "DesktopDuzenSabitleyici-win-exe.patch-from-1.0.11.bin", size = fcd.Length, sha256 = H(fcd) } } },
            ["win-setup"] = new { file = "DesktopDuzenSabitleyici-Setup-1.1.0.exe", size = 8, sha256 = H(files["DesktopDuzenSabitleyici-Setup-1.1.0.exe"]), patches = new { } },
        },
    });
    files["latest.json"] = Encoding.UTF8.GetBytes(manifest);
    int port = 18000 + Environment.ProcessId % 2000;
    string Base = $"http://127.0.0.1:{port}";
    var hits = new List<string>();
    using var http = new HttpListener();
    http.Prefixes.Add(Base + "/");
    http.Start();
    var cts = new CancellationTokenSource();
    _ = Task.Run(async () =>
    {
        while (!cts.IsCancelled())
        {
            HttpListenerContext ctx;
            try { ctx = await http.GetContextAsync(); } catch { break; }
            var path = ctx.Request.Url!.AbsolutePath;
            lock (hits) hits.Add(path);
            byte[] body; int st = 200;
            if (path == "/api/latest")
            {
                var assets = files.Select(f => new { name = f.Key, size = f.Value.Length, browser_download_url = $"{Base}/dl/v1.1.0/{f.Key}" });
                body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { tag_name = "v1.1.0", assets }));
            }
            else if (path == "/latest/latest.json") body = files["latest.json"];
            else if (path.StartsWith("/dl/v1.1.0/") && files.TryGetValue(Uri.UnescapeDataString(path["/dl/v1.1.0/".Length..]), out var b)) body = b;
            else { st = 404; body = Encoding.UTF8.GetBytes("not found"); }
            ctx.Response.StatusCode = st;
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
    });

    // --- eski istemci (1.0.11, git'teki kod birebir) ---
    OldDDS.OldApi.Url = Base + "/api/latest";
    var oi = await OldDDS.Updater.CheckAsync();
    Ok(oi != null && oi.Version == new Version(1, 1, 0), "1.0.11 yeni sürümü görür (v1.1.0 > 1.0.11)");
    Ok(oi?.Url.EndsWith("/DesktopDuzenSabitleyici.exe") == true, "1.0.11 tam exe'yi eski adıyla bulur");
    Ok(oi?.PatchUrl?.EndsWith("/DesktopDuzenSabitleyici-patch-from-1.0.11.bin") == true, "1.0.11 eski biçim (DDSD1) yamasını bulur");
    var oldExe = Path.Combine(tmp, "old-installed.exe");
    File.WriteAllBytes(oldExe, A);
    var viaLegacy = Path.Combine(tmp, "via-legacy.exe");
    OldDDS.Delta.Apply(oldExe, WriteTmp("legacy.bin", legacy), viaLegacy);
    Ok(File.ReadAllBytes(viaLegacy).AsSpan().SequenceEqual(B), "1.0.11'in yama uygulayıcısı (DDSD1) yeni exe'yi birebir üretir");
    // eski DownloadAsync: ProcessPath (test sunucusu) yamanın kaynağı değil → kendiliğinden tam dosyaya geçer
    var of = await OldDDS.Updater.DownloadAsync(oi!, (_, _, _) => { }, CancellationToken.None);
    Ok(File.ReadAllBytes(of).AsSpan().SequenceEqual(B), "1.0.11 yama tutmazsa tam exe'yi indirir (aynı bayt)");

    // --- yeni istemci ---
    Updater.ManifestUrl = Base + "/latest/latest.json";
    Updater.DownloadBase = Base + "/dl";
    var ni = Updater.Plan(manifest, new Version(1, 0, 11), "1.0.11")!;
    bool? mode = null;
    long lastDone = 0;
    var nf = await Updater.DownloadAsync(ni, (d, t) => lastDone = d, CancellationToken.None, pm => mode ??= pm, installedFile: oldExe);
    Ok(mode == true && File.ReadAllBytes(nf).AsSpan().SequenceEqual(B) && lastDone == fcd.Length, "FCD1 yaması ile güncelleme (yalnızca değişen kısımlar)");
    var wrong = WriteTmp("wrong-installed.exe", Encoding.ASCII.GetBytes("baska exe"));
    var modes = new List<bool>();
    var nf2 = await Updater.DownloadAsync(ni, (_, _) => { }, CancellationToken.None, pm => modes.Add(pm), installedFile: wrong);
    Ok(modes.SequenceEqual(new[] { true, false }) && File.ReadAllBytes(nf2).AsSpan().SequenceEqual(B), "yama tutmazsa tam dosyaya geçer, SHA-256 doğrulanır");
    var badInfo = ni with { PatchUrl = null, Sha256 = new string('0', 64) };
    bool threw = false;
    try { await Updater.DownloadAsync(badInfo, (_, _) => { }, CancellationToken.None); } catch (InvalidDataException) { threw = true; }
    Ok(threw, "SHA-256 uyuşmazsa güncelleme yapılmaz");
    cts.Cancel();
    http.Stop();
});

string WriteTmp(string name, byte[] b) { var p = Path.Combine(tmp, name); File.WriteAllBytes(p, b); return p; }

try { Directory.Delete(tmp, true); } catch { }
Console.WriteLine($"\n{pass} geçti, {fail} başarısız");
return fail == 0 ? 0 : 1;

static class CtsExt { public static bool IsCancelled(this CancellationTokenSource c) => c.IsCancellationRequested; }
