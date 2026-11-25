using DDS.Core;

namespace DDS;

/// <summary>Sürüm notları (uygulamaya gömülü; "neler yeni" penceresi bunları gösterir). En yeni en üstte.</summary>
public static class News
{
    public static readonly (string v, string tr, string en, string ar)[] Items =
    {
        ("1.2.0",
         "Ekran düzenleri artık anlaşılır adlarla görünür (Sadece dizüstü ekranı, Dizüstü + harici monitör gibi). Listede sağ tıklayıp istediğiniz adı verebilirsiniz. Hiç görülmemiş bir ekranda en çok benzeyen kayıt seçilir: aynı monitör, çözünürlük, oran ve ölçek dikkate alınır. Explorer yeniden başladığında düzen beklemeden geri gelir. Windows'un “Simgeleri otomatik düzenle” ve “Kılavuza hizala” ayarları açıksa uyarı çıkar ve tek tıkla kapatılabilir. Kayıt geçmişinde her kaydın küçük önizlemesi görünür. Program bilgisayar açılırken Windows'u yavaşlatmaz ve arka planda daha az bellek ile işlemci kullanır.",
         "Display setups now have clear names (Laptop screen only, Laptop + external monitor and so on). Right-click a record to give it your own name. On a screen never seen before, the most similar record is chosen by monitor, resolution, aspect ratio and scaling. When Explorer restarts, the layout comes back right away. If Windows “Auto arrange icons” or “Align icons to grid” is on, you get a warning and can turn it off with one click. Each record in the history shows a small preview. The program no longer slows Windows down at startup and uses less memory and CPU in the background.",
         "أصبحت إعدادات الشاشات تظهر بأسماء واضحة (شاشة الحاسوب المحمول فقط، الحاسوب المحمول + شاشة خارجية وغيرها). انقر بزر الفأرة الأيمن على أي سجل لتسميته كما تشاء. على شاشة لم تُرَ من قبل يُختار أقرب سجل حسب الشاشة والدقة ونسبة العرض والتحجيم. عند إعادة تشغيل المستكشف (Explorer) يعود الترتيب فوراً. إذا كان خيار «ترتيب الأيقونات تلقائياً» أو «محاذاة الأيقونات إلى الشبكة» مفعّلاً في ويندوز يظهر تنبيه ويمكن إيقافه بنقرة واحدة. يعرض كل سجل في السجل معاينة مصغّرة. لم يعد البرنامج يُبطئ بدء تشغيل ويندوز، ويستهلك ذاكرة ومعالجاً أقل في الخلفية."),
        ("1.1.3",
         "Programın adı artık Desktop Simge Sabitleyici. Masaüstü ve Başlat menüsündeki kısayollar yeni adla aynı yerinde kalır; tüm kayıtlarınız, ayarlarınız ve eşitlemeniz olduğu gibi korunur.",
         "The program is now called Desktop Icon Keeper (Desktop Simge Sabitleyici). Desktop and Start menu shortcuts keep their place under the new name; all your records, settings and sync are kept as they are.",
         "أصبح اسم البرنامج الآن «مثبّت أيقونات سطح المكتب» (Desktop Simge Sabitleyici). تبقى الاختصارات على سطح المكتب وفي قائمة ابدأ في مكانها بالاسم الجديد، وتُحفظ جميع سجلاتك وإعداداتك ومزامنتك كما هي."),
        ("1.1.2",
         "Güncellemeler artık tamamen kendiliğinden: yeni sürüm bulununca arka planda indirilir, pencere açmadan kurulur ve program birkaç saniyede yeniden açılır. Kurulum gereken güncellemeler de sessiz yapılır. Kayıtlar korunur.",
         "Updates are now fully automatic: when a new version is found it downloads in the background, installs without any window and the program reopens within seconds. Updates that need the installer also run silently. Records are kept.",
         "أصبحت التحديثات تلقائية بالكامل: عند العثور على إصدار جديد يُنزَّل في الخلفية ويُثبَّت دون أي نافذة ويُعاد فتح البرنامج خلال ثوانٍ. التحديثات التي تحتاج إلى برنامج التثبيت تتم بصمت أيضاً. تبقى السجلات محفوظة."),
        ("1.1.1",
         "Arapça: Ayarlar'daki Asistan Agent düğmesi artık çevrili",
         "Arabic: the Assistant Agent button in Settings is now translated",
         "العربية: أصبح زر الوكيل المساعد في الإعدادات مترجماً"),
        ("1.1.0",
         "Yeni Falcon görünümü (Inter yazı tipi, yuvarlak kartlar); Ayarlar → Görünüm'de 8 renk şeması (eski görünüm: Klasik), koyu / açık / otomatik tema ve yazı boyutu\n" +
         "Güncellemeler ortak sisteme geçti: latest.json + yalnızca değişen kısımlar (FCD1), SHA-256 doğrulaması\n" +
         "Kurulum programı (DesktopDuzenSabitleyici-Setup.exe): kullanıcıya özel, yönetici izni gerekmez\n" +
         "Kayıtlar artık hiç silinmez: taşınan, eski ve eşitlemede çakışan kayıtlar Arşiv'de durur, geri alınabilir\n" +
         "Admin ile iletişim (öneri / hata), otomatik hata raporu (günde en çok 5), ilk açılış rehberi\n" +
         "Sağ üstte tema ve dil düğmeleri; Orta ve Tam ekran ayrı düğmeler",
         "New Falcon look (Inter font, rounded cards); 8 colour schemes in Settings → Look (previous look: Classic), dark / light / auto theme and font size\n" +
         "Updates moved to the shared system: latest.json + changed parts only (FCD1), SHA-256 verification\n" +
         "Installer (DesktopDuzenSabitleyici-Setup.exe): per user, no admin rights needed\n" +
         "Records are never deleted any more: removed, older and sync-conflicting records stay in the Archive and can be brought back\n" +
         "Contact the admin (suggestion / bug), automatic error reports (at most 5 a day), first-run guide\n" +
         "Theme and language buttons at the top right; separate Half-size and Full-screen buttons",
         "مظهر Falcon الجديد (خط Inter وبطاقات مستديرة)؛ 8 أنظمة ألوان في الإعدادات ← المظهر (المظهر السابق: الكلاسيكي)، سمة داكنة / فاتحة / تلقائية وحجم الخط\n" +
         "انتقلت التحديثات إلى النظام المشترك: latest.json + الأجزاء المتغيرة فقط (FCD1) مع التحقق عبر SHA-256\n" +
         "برنامج تثبيت (DesktopDuzenSabitleyici-Setup.exe): لكل مستخدم، دون صلاحيات المسؤول\n" +
         "لا تُحذف السجلات بعد الآن: السجلات المزالة والقديمة والمتعارضة تبقى في الأرشيف ويمكن استعادتها\n" +
         "التواصل مع المسؤول (اقتراح / خطأ)، تقارير أخطاء تلقائية (5 يومياً كحد أقصى)، دليل التشغيل الأول\n" +
         "زرا السمة واللغة في أعلى اليمين؛ زران منفصلان للحجم المتوسط وملء الشاشة"),
    };

    public static string? For(string v)
    {
        foreach (var n in Items)
            if (n.v == v) return Lang.Current switch { "en" => n.en, "ar" => n.ar, _ => n.tr };
        return null;
    }
}

static class FormKit
{
    public static int D(Control c, int v) => (int)Math.Round(v * c.DeviceDpi / 96.0);

    public static Label Lbl(string text, float size = 10f, bool semi = false, Color? color = null) => new()
    {
        Text = text, AutoSize = true, Font = Theme.F(size, semi), ForeColor = color ?? Theme.Text, BackColor = Color.Transparent,
        Margin = new Padding(0, 4, 0, 4),
    };

    /// <summary>Sarmalayan etiket: genişliği akış panelinden alır.</summary>
    public static Label Wrap(string text, int width, float size = 10f, Color? color = null)
    {
        var l = Lbl(text, size, false, color);
        l.MaximumSize = new Size(width, 0);
        return l;
    }

    public static FlowLayoutPanel Col() => new()
    {
        FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill, BackColor = Theme.Card,
    };

    public static FlowLayoutPanel Row() => new()
    {
        FlowDirection = Lang.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, WrapContents = true, AutoSize = true,
        BackColor = Theme.Card, Margin = new Padding(0, 4, 0, 4),
    };

    /// <summary>Alt düğme çubuğu.</summary>
    public static FlowLayoutPanel Bar() => new()
    {
        Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = Lang.IsRtl ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
        Padding = new Padding(16, 6, 16, 12), BackColor = Theme.Card,
    };

    public static Button B(string text, Btn kind, Action click)
    {
        var b = Theme.ActionButton(text, kind);
        b.Margin = new Padding(6, 4, 6, 4);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Durum kutusu (ok / warn / danger).</summary>
    public static Label Notice(string text, string kind, int width)
    {
        var l = Wrap(text, width, 9.5f, Look.Tok(kind + "-text", Theme.Card));
        l.BackColor = Look.Tok(kind + "-soft", Theme.Card);
        l.Padding = new Padding(10, 8, 10, 8);
        l.Margin = new Padding(0, 10, 0, 4);
        return l;
    }
}

/// <summary>"Bu sürümde neler yeni" — güncellemeden sonraki ilk açılışta bir kez.</summary>
public sealed class WhatsNewForm : Form
{
    public WhatsNewForm(string version, string? notes)
    {
        Text = CoreStrings.T("fc.wn.title");
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.Dialog(this);
        ClientSize = new Size(FormKit.D(this, 600), FormKit.D(this, 460));
        int w = FormKit.D(this, 548);
        var col = FormKit.Col();
        col.Padding = new Padding(FormKit.D(this, 24), FormKit.D(this, 18), FormKit.D(this, 24), 0);
        col.Controls.Add(FormKit.Lbl("✦ " + CoreStrings.T("fc.wn.title"), 14f, true));
        col.Controls.Add(FormKit.Lbl(CoreStrings.T1("fc.wn.ver", "v", version), 9.5f, false, Theme.Sub));
        var lines = (notes ?? "").Split('\n').Select(x => x.Trim().TrimStart('-', '*', '•').Trim()).Where(x => x.Length > 0).ToList();
        if (lines.Count == 0) lines.Add(CoreStrings.T("fc.wn.none"));
        foreach (var l in lines)
        {
            var lb = FormKit.Wrap((lines.Count > 1 ? "•  " : "") + l, w, 10f);
            lb.Margin = new Padding(0, 6, 0, 0);
            col.Controls.Add(lb);
        }
        col.Controls.Add(FormKit.Notice("✓  " + CoreStrings.T("fc.wn.safe"), "ok", w));
        var bar = FormKit.Bar();
        var ok = FormKit.B(CoreStrings.T("fc.ok"), Btn.Primary, Close);
        bar.Controls.Add(ok);
        AcceptButton = ok;
        Controls.Add(col);
        Controls.Add(bar);
    }

    /// <summary>Notlar: önce uygulamaya gömülü News, yoksa güncelleyicinin bıraktığı latest.json notları.</summary>
    public static string? NotesFor(string v)
    {
        var n = News.For(v);
        if (n != null) return n;
        try
        {
            var p = CoreState.Get("whatsnew.pending");
            if ((string?)p?["version"] == v && p?["notes"] is System.Text.Json.Nodes.JsonObject o)
                foreach (var k in new[] { Lang.Current, "tr", "en", "ar" })
                    if ((string?)o[k] is { Length: > 0 } s) return s;
        }
        catch { }
        return null;
    }
}

/// <summary>İlk açılış rehberi: dil → görünüm (şema, tema, yazı boyutu) → hazır. Seçimler hemen uygulanır.</summary>
public sealed class OnboardingForm : Form
{
    readonly Action<string?, string?, string?, double?> apply;
    int step;
    const int Steps = 3;

    /// <param name="apply">(dil, şema, mod, yazı ölçeği) — null olan değişmez. Ana pencere kendini yeniden kurar.</param>
    public OnboardingForm(Action<string?, string?, string?, double?> apply)
    {
        this.apply = apply;
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Render();
    }

    /// <summary>Sonraki adım (ekran görüntüsü kipi görünüm adımını gösterir).</summary>
    public void Next() { if (step < Steps - 1) { step++; Render(); } }

    void Render()
    {
        SuspendLayout();
        foreach (Control c in Controls.Cast<Control>().ToList()) { Controls.Remove(c); c.Dispose(); }
        Text = CoreStrings.T("fc.ob.title");
        Theme.Dialog(this);
        Theme.DarkTitle(this);
        ClientSize = new Size(FormKit.D(this, 660), FormKit.D(this, step == 1 ? 680 : 520));
        int w = FormKit.D(this, 588);
        var col = FormKit.Col();
        col.Padding = new Padding(FormKit.D(this, 24), FormKit.D(this, 18), FormKit.D(this, 24), 0);
        Theme.DarkScroll(col);
        col.Controls.Add(FormKit.Lbl(CoreStrings.T("fc.ob.step", new Dictionary<string, object?> { ["i"] = step + 1, ["n"] = Steps }), 9f, true, Theme.Accent));

        if (step == 0)
        {
            col.Controls.Add(FormKit.Lbl(CoreStrings.T("fc.ob.welcome"), 16f, true));
            col.Controls.Add(FormKit.Wrap(Lang.T("ob_app"), w, 10f, Theme.Sub));
            var h = FormKit.Lbl(CoreStrings.T("fc.lang"), 11f, true); h.Margin = new Padding(0, 18, 0, 2);
            col.Controls.Add(h);
            col.Controls.Add(FormKit.Wrap(CoreStrings.T("fc.ob.langDesc"), w, 9.5f, Theme.Sub));
            var row = FormKit.Row();
            foreach (var (code, name) in Lang.Languages)
            {
                var b = FormKit.B(name, code == Lang.Current ? Btn.Primary : Btn.Secondary, () => { apply(code, null, null, null); Render(); });
                b.MinimumSize = new Size(FormKit.D(this, 150), FormKit.D(this, 42));
                b.Font = Theme.F(11f, true);
                row.Controls.Add(b);
            }
            col.Controls.Add(row);
        }
        else if (step == 1)
        {
            col.Controls.Add(FormKit.Lbl(CoreStrings.T("fc.ob.lookTitle"), 16f, true));
            col.Controls.Add(FormKit.Wrap(CoreStrings.T("fc.ob.lookDesc") + " " + Lang.T("look_preview"), w, 10f, Theme.Sub));
            AddLookControls(col, w, (s, m, x) => { apply(null, s, m, x); Render(); });
        }
        else
        {
            col.Controls.Add(FormKit.Lbl("✓ " + CoreStrings.T("fc.ob.doneTitle"), 16f, true));
            col.Controls.Add(FormKit.Wrap(CoreStrings.T("fc.ob.doneDesc"), w, 10f, Theme.Sub));
            col.Controls.Add(FormKit.Notice(Lang.T("tray_bg"), "ok", w));
        }

        var bar = FormKit.Bar();
        if (step < Steps - 1)
        {
            var next = FormKit.B(CoreStrings.T("fc.next"), Btn.Primary, () => { step++; Render(); });
            bar.Controls.Add(next);
            AcceptButton = next;
            bar.Controls.Add(FormKit.B(CoreStrings.T("fc.skip"), Btn.Ghost, () => { DialogResult = DialogResult.OK; Close(); }));
        }
        else
        {
            var done = FormKit.B(CoreStrings.T("fc.ob.start"), Btn.Primary, () => { DialogResult = DialogResult.OK; Close(); });
            bar.Controls.Add(done);
            AcceptButton = done;
        }
        if (step > 0) bar.Controls.Add(FormKit.B(CoreStrings.T("fc.back"), Btn.Secondary, () => { step--; Render(); }));
        Controls.Add(col);
        Controls.Add(bar);
        ResumeLayout(true);
        Invalidate(true);
    }

    /// <summary>Şema seçici + koyu/açık/otomatik + yazı boyutu (Ayarlar → Görünüm de bunu kullanır).</summary>
    public static void AddLookControls(FlowLayoutPanel col, int w, Action<string?, string?, double?> set)
    {
        var hs = FormKit.Lbl(CoreStrings.T("fc.scheme"), 10f, true, Theme.Label); hs.Margin = new Padding(0, 14, 0, 2);
        col.Controls.Add(hs);
        var grid = FormKit.Row();
        grid.MaximumSize = new Size(w, 0);
        foreach (var id in CoreTokens.Order)
        {
            var b = new SchemeChip(id, id == Look.Scheme) { Margin = new Padding(0, 0, 8, 8) };
            b.Click += (_, _) => set(id, null, null);
            grid.Controls.Add(b);
        }
        col.Controls.Add(grid);

        var hm = FormKit.Lbl(CoreStrings.T("fc.theme"), 10f, true, Theme.Label); hm.Margin = new Padding(0, 10, 0, 2);
        col.Controls.Add(hm);
        var modes = FormKit.Row();
        foreach (var (m, glyph) in new[] { ("dark", "☾"), ("light", "☀"), ("auto", "◐") })
            modes.Controls.Add(FormKit.B(glyph + "  " + CoreStrings.T("fc.theme." + m), m == Look.Mode ? Btn.Primary : Btn.Secondary, () => set(null, m, null)));
        col.Controls.Add(modes);

        var hf = FormKit.Lbl(CoreStrings.T("fc.font") + "  ·  %" + Math.Round(Look.Scale * 100), 10f, true, Theme.Label); hf.Margin = new Padding(0, 10, 0, 2);
        col.Controls.Add(hf);
        var fr = FormKit.Row();
        fr.Controls.Add(FormKit.B("A−", Btn.Secondary, () => set(null, null, Look.Scale - Look.Step)));
        fr.Controls.Add(FormKit.B(CoreStrings.T("fc.font.reset"), Btn.Secondary, () => set(null, null, 1.0)));
        fr.Controls.Add(FormKit.B("A+", Btn.Secondary, () => set(null, null, Look.Scale + Look.Step)));
        col.Controls.Add(fr);
    }
}

/// <summary>Şema seçici kutusu: şemanın zemin / kart / vurgu renkleriyle küçük önizleme + ad.</summary>
public sealed class SchemeChip : Control
{
    readonly string id;
    readonly bool selected;
    bool hover;

    public SchemeChip(string id, bool selected)
    {
        this.id = id; this.selected = selected;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = Theme.F(9f, true);
        Size = new Size((int)(136 * DeviceDpi / 96.0), (int)(64 * DeviceDpi / 96.0));
        AccessibleName = Look.SchemeLabel(id);
        AccessibleRole = AccessibleRole.RadioButton;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        var set = CoreTokens.Schemes[id][Look.IsDark ? 0 : 1];
        Color C(string k) { var c = Look.Parse(set[k]); return c.A == 255 ? c : Look.Blend(c, Look.Parse(set["bg"])); }
        float s = DeviceDpi / 96f;
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        using (var path = Theme.Round(r, 10 * s))
        {
            using (var b = new SolidBrush(C("bg"))) g.FillPath(b, path);
            var inner = new RectangleF(8 * s, 8 * s, Width - 16 * s, 22 * s);
            using (var ip = Theme.Round(inner, 6 * s))
            using (var b = new SolidBrush(C("panel-solid"))) g.FillPath(b, ip);
            using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(12 * s, 14 * s, 40 * s, 10 * s), C("primary-1"), C("primary-2"), 0f))
            using (var bp = Theme.Round(new RectangleF(12 * s, 14 * s, 40 * s, 10 * s), 5 * s)) g.FillPath(b, bp);
            using (var b = new SolidBrush(C("accent"))) g.FillEllipse(b, Width - 28 * s, 14 * s, 10 * s, 10 * s);
            var tr = new Rectangle((int)(8 * s), (int)(34 * s), Width - (int)(16 * s), Height - (int)(38 * s));
            TextRenderer.DrawText(g, Look.SchemeLabel(id), Font, tr, C("text"), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                (Lang.IsRtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0));
            using var pen = new Pen(selected ? Theme.Accent : hover ? Theme.BorderStrong : Theme.Border, selected ? 2.5f * s : 1f);
            g.DrawPath(pen, path);
        }
    }
}

/// <summary>"Admin ile iletişim": öneri / hata bildirimi tek gelen kutusuna (Inbox). İnternet yoksa kuyruğa alınır.</summary>
public sealed class ContactForm : Form
{
    readonly ComboBox cType;
    readonly TextBox tMsg, tReach;
    readonly Label lStatus;
    readonly Button bSend;

    public ContactForm()
    {
        Text = CoreStrings.T("fc.contact.title");
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.Dialog(this);
        ClientSize = new Size(FormKit.D(this, 580), FormKit.D(this, 540));
        int w = FormKit.D(this, 528);
        var col = FormKit.Col();
        col.Padding = new Padding(FormKit.D(this, 24), FormKit.D(this, 18), FormKit.D(this, 24), 0);
        col.Controls.Add(FormKit.Lbl("✉ " + CoreStrings.T("fc.contact.title"), 14f, true));
        col.Controls.Add(FormKit.Wrap(CoreStrings.T("fc.contact.desc"), w, 10f, Theme.Sub));
        col.Controls.Add(FormKit.Lbl(CoreStrings.T("fc.contact.type"), 10f, true, Theme.Label));
        cType = Theme.Combo();
        cType.Width = FormKit.D(this, 260);
        cType.Items.AddRange(new object[] { CoreStrings.T("fc.contact.oneri"), CoreStrings.T("fc.contact.hata") });
        cType.SelectedIndex = 0;
        col.Controls.Add(cType);
        col.Controls.Add(FormKit.Lbl(CoreStrings.T("fc.contact.msg"), 10f, true, Theme.Label));
        tMsg = Theme.Box(true);
        tMsg.MaxLength = 5000;
        tMsg.Size = new Size(w, FormKit.D(this, 150));
        tMsg.Font = Theme.F(10f);
        col.Controls.Add(tMsg);
        col.Controls.Add(FormKit.Lbl(CoreStrings.T("fc.contact.reach"), 10f, true, Theme.Label));
        tReach = Theme.Box();
        tReach.MaxLength = 160;
        tReach.Width = w;
        tReach.RightToLeft = RightToLeft.No;
        col.Controls.Add(tReach);
        col.Controls.Add(FormKit.Wrap(CoreStrings.T("fc.contact.privacy"), w, 8.5f, Theme.Sub));
        lStatus = FormKit.Wrap(Inbox.QueueCount > 0 ? CoreStrings.T1("fc.contact.waiting", "n", Inbox.QueueCount) : "", w, 9.5f, Theme.Sub);
        col.Controls.Add(lStatus);

        var bar = FormKit.Bar();
        bSend = FormKit.B("✉ " + CoreStrings.T("fc.contact.send"), Btn.Primary, async () => await SendAsync());
        bar.Controls.Add(bSend);
        bar.Controls.Add(FormKit.B(CoreStrings.T("fc.close"), Btn.Secondary, Close));
        Controls.Add(col);
        Controls.Add(bar);
    }

    async Task SendAsync()
    {
        var msg = tMsg.Text.Trim();
        if (msg.Length == 0) { lStatus.Text = CoreStrings.T("fc.contact.empty"); lStatus.ForeColor = Theme.Warn; return; }
        bSend.Enabled = false;
        lStatus.ForeColor = Theme.Sub;
        lStatus.Text = CoreStrings.T("fc.contact.sending");
        try
        {
            var r = await Inbox.Send(cType.SelectedIndex == 1 ? "hata" : "oneri", msg, tReach.Text.Trim());
            if (IsDisposed) return;
            tMsg.Text = "";
            lStatus.ForeColor = r == "sent" ? Theme.GreenText : Theme.Warn;
            lStatus.Text = CoreStrings.T(r == "sent" ? "fc.contact.ok" : "fc.contact.queued");
        }
        catch { lStatus.Text = CoreStrings.T("fc.contact.queued"); }
        finally { if (!IsDisposed) bSend.Enabled = true; }
    }
}

/// <summary>Arşiv: hiçbir kayıt silinmez; buradan geçmişe geri alınır.</summary>
public sealed class ArchiveForm : Form
{
    readonly StoreData store;
    readonly ListView lv;
    public bool Changed { get; private set; }

    public ArchiveForm(StoreData store)
    {
        this.store = store;
        Text = Lang.T("archive_title");
        Icon = Theme.AppIcon();
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.Dialog(this);
        ClientSize = new Size(FormKit.D(this, 820), FormKit.D(this, 520));
        var top = FormKit.Col();
        top.Dock = DockStyle.Top;
        top.AutoSize = true;
        top.Padding = new Padding(FormKit.D(this, 20), FormKit.D(this, 14), FormKit.D(this, 20), FormKit.D(this, 6));
        top.Controls.Add(FormKit.Lbl("🗄 " + Lang.T("archive_title"), 14f, true));
        top.Controls.Add(FormKit.Wrap(Lang.T("archive_desc"), FormKit.D(this, 770), 9.5f, Theme.Sub));
        lv = new ListView
        {
            View = View.Details, FullRowSelect = true, MultiSelect = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            BackColor = Theme.Input, ForeColor = Theme.Text, HeaderStyle = ColumnHeaderStyle.Nonclickable, RightToLeftLayout = Lang.IsRtl,
        };
        Theme.DarkScroll(lv);
        lv.Columns.Add(Lang.T("col_display"), FormKit.D(this, 280));
        lv.Columns.Add(Lang.T("col_icons"), FormKit.D(this, 60));
        lv.Columns.Add(Lang.T("col_date"), FormKit.D(this, 130));
        lv.Columns.Add(Lang.T("col_reason"), FormKit.D(this, 170));
        lv.Columns.Add(Lang.T("col_archived"), FormKit.D(this, 130));
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(FormKit.D(this, 20), 0, FormKit.D(this, 20), 0), BackColor = Theme.Card };
        pad.Controls.Add(lv);
        var bar = FormKit.Bar();
        bar.Controls.Add(FormKit.B(CoreStrings.T("fc.close"), Btn.Secondary, Close));
        bar.Controls.Add(FormKit.B("↩ " + Lang.T("archive_restore"), Btn.Primary, Restore));
        Controls.Add(pad);
        Controls.Add(top);
        Controls.Add(bar);
        Fill();
    }

    void Fill()
    {
        lv.BeginUpdate();
        lv.Items.Clear();
        foreach (var a in store.Archive.OrderByDescending(a => a.ArchivedUtc))
        {
            var it = new ListViewItem(a.Display.Length > 0 ? a.Display : a.Key) { Tag = a.Snap.Id, ForeColor = a.Restored ? Theme.Sub : Theme.Text };
            it.SubItems.Add(a.Snap.Icons.Count.ToString());
            it.SubItems.Add(a.Snap.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
            it.SubItems.Add(Lang.T("reason_" + a.Reason) + (a.Restored ? " · " + Lang.T("reason_restored") : ""));
            it.SubItems.Add(a.ArchivedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
            lv.Items.Add(it);
        }
        if (lv.Items.Count == 0) lv.Items.Add(new ListViewItem(Lang.T("archive_empty")) { ForeColor = Theme.Sub });
        lv.EndUpdate();
    }

    void Restore()
    {
        if (lv.SelectedItems.Count == 0 || lv.SelectedItems[0].Tag is not string id) return;
        if (store.RestoreFromArchive(id) == null) return;
        Storage.SaveStore(store);
        Changed = true;
        Fill();
    }
}
