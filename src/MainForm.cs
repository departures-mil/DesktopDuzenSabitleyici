using System.Diagnostics;
using DDS.Core;
using DDS.Native;
using Microsoft.Win32;
using WinTimer = System.Windows.Forms.Timer;

namespace DDS;

public sealed class MainForm : Form
{
    readonly Settings settings;
    readonly StoreData store;
    /// <summary>Ekran görüntüsü kipi (CI): koruma, tepsi, güncelleme, agent çalışmaz; diske yazılmaz.</summary>
    readonly bool shot;
    readonly Guardian guard;
    readonly NotifyIcon tray;
    readonly ToolTip tips = new() { InitialDelay = 300 };
    readonly EventWaitHandle showEvent;
    readonly List<(DateTime t, string key, object[] args)> logs = new();
    readonly WinTimer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    readonly WinTimer autoSyncTimer = new() { Interval = 5 * 60 * 1000 };
    readonly WinTimer usageTimer = new() { Interval = 30000 };
    readonly WinTimer inboxTimer = new() { Interval = 5 * 60 * 1000 };
    /// <summary>Düşük kaynak: pencere gizliyken 15 dakikada bir kullanılmayan belleği Windows'a geri verir.</summary>
    readonly WinTimer trimTimer = new() { Interval = 15 * 60 * 1000 };
    static readonly int WM_TASKBARCREATED = OperatingSystem.IsWindows() ? Win32.RegisterWindowMessage("TaskbarCreated") : -1;
    EventWaitHandle? quitEvent;
    enum WinMode { Full, Max, Half }
    WinMode winMode = WinMode.Full;
    bool introDone;

    bool startHidden, reallyExit, loading = true, started, syncing, shutdownSaved;
    bool half => winMode == WinMode.Half;
    DateTime? lastSyncedSave;
    UpdateInfo? update;

    // UI
    Panel titleBar = null!; Label titleLbl = null!; PictureBox logo = null!;
    Button bUpdate = null!, bSync = null!, bMin = null!, bHalf = null!, bFull = null!, bClose = null!, bTheme = null!, bLang = null!;
    TableLayoutPanel content = null!;
    Label hStatus = null!, lStatus = null!, lHow = null!, lScreensH = null!, lScreens = null!, lIcons = null!, lSave = null!, lRestore = null!;
    Label lProfile = null!, lArr = null!;
    Button bArrFix = null!;
    FlowLayoutPanel arrRow = null!;
    ContextMenuStrip lvMenu = null!;
    ToolStripMenuItem miRename = null!;
    Button bProtect = null!, bSaveNow = null!, bRestore = null!, bPreviewNow = null!;
    Label hSettings = null!, lMode = null!, lLang = null!;
    Button bContact = null!, bArchive = null!, bCheckUpd = null!, bWhatsNew = null!, bOnboard = null!;
    Label hLook = null!, lLookDesc = null!, hCrash = null!, lCrash = null!, hAbout = null!, lAbout = null!;
    CheckBox cCrash = null!;
    Button bAgent = null!, bAgentTop = null!;
    FcAgent.Agent? agent;
    Button bBell = null!;
    Announcements announcements = null!;
    CheckBox cAuto = null!, cStartup = null!, cAutoSave = null!;
    Label lSaveInfo = null!, lModeInfo = null!;
    Label lUsageH = null!, lUsage = null!;
    ComboBox cLang = null!;
    Label hProfiles = null!, hLog = null!, hPreview = null!, lPvInfo = null!;
    LayoutPreview preview = null!;
    ListView lv = null!;
    Button bDel = null!, bRestoreSel = null!, bPreviewSel = null!;
    ListBox lb = null!;
    ContextMenuStrip trayMenu = null!;
    FlowLayoutPanel statusFlow = null!, settingsFlow = null!;
    ToolStripMenuItem miOpen = null!, miSave = null!, miRestore = null!, miExit = null!;

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public MainForm(bool startHidden, Settings? s = null, StoreData? st = null, bool screenshot = false)
    {
        this.startHidden = startHidden;
        shot = screenshot;
        settings = s ?? Storage.LoadSettings();
        store = st ?? Storage.LoadStore();
        Lang.Current = settings.Lang;
        CoreStrings.Lang = () => Lang.Current;
        Look.SystemLight = SystemLight;
        Look.Set(settings.Scheme, settings.Theme, settings.FontScale, notify: false);

        Text = Lang.T("app_title");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Icon = Theme.AppIcon();
        DoubleBuffered = true;
        MinimumSize = new Size(D(780), D(540));
        StyleForm();

        BuildUI();

        tray = new NotifyIcon { Icon = Icon, Visible = !shot, Text = Installer.DisplayName, ContextMenuStrip = trayMenu };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };

        if (!shot) try
        {
            agent = new FcAgent.Agent("desktop-duzen", Updater.CurrentText, () => Lang.Current);
            agent.Start(this, tray);
            agent.OnRedraw(() => { if (IsHandleCreated) BeginInvoke(UpdateAgentButton); });
            UpdateAgentButton();
        }
        catch { agent = null; }

        guard = new Guardian(store, settings);
        guard.Log += OnLog;
        guard.Changed += RefreshInfo;
        guard.AutoArrangeDetected += () =>
        {
            if (!shot) tray.ShowBalloonTip(6000, Lang.T("app_title"), Lang.T("arr_auto"), ToolTipIcon.Warning);
        };

        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, shot ? null : Program.ShowEventName);
        if (!shot)
        {
            ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) =>
            {
                try { if (IsHandleCreated) BeginInvoke(ShowMain); } catch { }
            }, null, -1, false);
            // kurulum programı çalışan kopyayı kibarca kapatır (DesktopDuzenSabitleyici.exe --quit)
            quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.QuitEventName);
            ThreadPool.RegisterWaitForSingleObject(quitEvent, (_, _) =>
            {
                try { if (IsHandleCreated) BeginInvoke(ExitApp); } catch { }
            }, null, -1, true);
            Installer.SetStartup(settings.StartWithWindows);
            Installer.TouchUninstallEntry();
            if (Installer.MigrateName(store)) Storage.SaveStore(store);
            SystemEvents.UserPreferenceChanged += OnSystemPrefs;
        }
        updateTimer.Tick += async (_, _) => await CheckUpdateSilently();
        autoSyncTimer.Tick += async (_, _) => await AutoSync();
        usageTimer.Tick += (_, _) => UpdateUsage();
        inboxTimer.Tick += async (_, _) => await Inbox.FlushAsync();
        trimTimer.Tick += (_, _) => { if (!Visible) TrimMemory(); };

        ApplyLanguage();
        loading = false;
    }

    // ---------------- window behaviour ----------------

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x20000; // WS_MINIMIZEBOX: taskbar click minimizes / restores
            return cp;
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        if (startHidden && !IsHandleCreated)
        {
            CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitle(this);
        if (shot) return;
        Win32.RegisterHotKey(Handle, 1, 0x0001 | 0x0002 | 0x4000, (uint)Keys.S); // Ctrl+Alt+S (no repeat)
        if (started) return;
        started = true;
        BeginInvoke(async () =>
        {
            guard.Start();
            RefreshInfo();
            updateTimer.Start();
            autoSyncTimer.Start();
            usageTimer.Start();
            inboxTimer.Start();
            trimTimer.Start();
            if (!startHidden) Intro();
            // Bilgisayar açılırken (--tray) koruma hemen çalışır; güncelleme denetimi, gelen kutusu ve duyurular
            // Windows'un açılışını yavaşlatmamak için 45 saniye sonra başlar.
            bool boot = startHidden;
            await Task.Delay(boot ? 45000 : 4000);
            if (boot && !Visible) TrimMemory();
            _ = Inbox.FlushAsync();
            await CheckUpdateSilently();
            await announcements.StartAsync();
        });
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyFull();
    }

    /// <summary>Tam ekran (her açılışta): ekranın tamamı.</summary>
    void ApplyFull()
    {
        winMode = WinMode.Full;
        WindowState = FormWindowState.Normal;
        Padding = Padding.Empty;
        Bounds = shot ? new Rectangle(0, 0, D(1600), D(940)) : Screen.FromPoint(Cursor.Position).Bounds;
        UpdateHalfGlyph();
    }

    /// <summary>Büyütülmüş: görev çubuğu görünür kalır.</summary>
    void ApplyMax()
    {
        winMode = WinMode.Max;
        WindowState = FormWindowState.Normal;
        Padding = Padding.Empty;
        Bounds = Screen.FromControl(this).WorkingArea;
        UpdateHalfGlyph();
    }

    void ToggleFull() { if (winMode == WinMode.Full) ApplyMax(); else ApplyFull(); }

    void ApplyHalf()
    {
        winMode = WinMode.Half;
        WindowState = FormWindowState.Normal;
        Padding = new Padding(D(5)); // free edge so the window can be resized with the mouse
        var wa = Screen.FromControl(this).WorkingArea;
        int w = Math.Max(MinimumSize.Width, wa.Width * 3 / 5), h = Math.Max(MinimumSize.Height, wa.Height * 7 / 10);
        Bounds = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
        UpdateHalfGlyph();
    }

    void ToggleHalf() { if (half) ApplyFull(); else ApplyHalf(); }

    void UpdateHalfGlyph()
    {
        if (bHalf == null) return;
        bHalf.Text = half ? Theme.GMax : Theme.GRestore;
        tips.SetToolTip(bHalf, CoreStrings.T(half ? "fc.win.restore" : "fc.win.half"));
        bFull.Text = winMode == WinMode.Full ? Theme.GBackFull : Theme.GFull;
        tips.SetToolTip(bFull, CoreStrings.T("fc.win.fullscreen"));
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84, WM_QUERYENDSESSION = 0x11, WM_HOTKEY = 0x312;
        if (m.Msg == WM_QUERYENDSESSION && !shutdownSaved)
        {
            shutdownSaved = true;
            guard.SaveBeforeShutdown("shutdown", "log_shutdown");
        }
        if (m.Msg == WM_TASKBARCREATED && WM_TASKBARCREATED > 0 && !shot)
            guard.ExplorerRestarted(); // Explorer yeniden başladı: düzeni geri getir
        if (m.Msg == WM_HOTKEY && (int)m.WParam == 1)
        {
            guard.SaveNow("hotkey");
            OnLog("log_hotkey", Array.Empty<object>());
            tray.ShowBalloonTip(1500, Lang.T("app_title"), Lang.T("log_hotkey"), ToolTipIcon.Info);
        }
        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST && half && WindowState == FormWindowState.Normal)
        {
            var p = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
            int g = D(6);
            bool l = p.X < g, r = p.X > ClientSize.Width - g, t = p.Y < g, b = p.Y > ClientSize.Height - g;
            int hit = t && l ? 13 : t && r ? 14 : b && l ? 16 : b && r ? 17 : l ? 10 : r ? 11 : t ? 12 : b ? 15 : 0;
            if (hit != 0) m.Result = hit;
        }
    }

    void DragWindow(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (e.Clicks == 2) { ToggleHalf(); return; }
        if (!half) return;
        Win32.ReleaseCapture();
        Win32.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
    }

    void ShowMain()
    {
        startHidden = false;
        if (!Visible) { Show(); if (!half) ApplyFull(); }
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        if (!introDone && started) BeginInvoke(Intro);
    }

    void HideToTray()
    {
        Hide();
        BeginInvoke(async () => { await Task.Delay(5000); if (!Visible) TrimMemory(); });
        if (!settings.TrayHintShown)
        {
            tray.ShowBalloonTip(4000, Lang.T("app_title"), Lang.T("tray_bg"), ToolTipIcon.Info);
            settings.TrayHintShown = true;
            Storage.SaveSettings(settings);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        if (e.CloseReason == CloseReason.WindowsShutDown && !shutdownSaved)
        {
            shutdownSaved = true;
            guard.SaveBeforeShutdown("shutdown", "log_shutdown");
        }
        try { Win32.UnregisterHotKey(Handle, 1); } catch { }
        SystemEvents.UserPreferenceChanged -= OnSystemPrefs;
        tray.Visible = false;
        guard.Dispose();
        trimTimer.Dispose();
        base.OnFormClosing(e);
    }

    void ExitApp()
    {
        if (settings.Protect && !shutdownSaved) { shutdownSaved = true; guard.SaveBeforeShutdown("shutdown", "log_saved"); }
        reallyExit = true;
        Close();
    }

    // ---------------- UI construction ----------------

    Button WinButton(string glyph, Action click, bool danger = false)
    {
        var b = new Button
        {
            Text = glyph, Font = Fonts.Icon(10f), Width = D(50), Height = D(46),
            FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text, BackColor = Theme.TitleBg,
            Margin = Padding.Empty, TabStop = false, Cursor = Cursors.Hand, UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = danger ? Color.FromArgb(232, 17, 35) : Theme.Hover;
        b.FlatAppearance.MouseDownBackColor = danger ? Color.FromArgb(200, 15, 30) : Look.Tok("active-line", Theme.TitleBg);
        if (danger) { b.MouseEnter += (_, _) => b.ForeColor = Color.White; b.MouseLeave += (_, _) => b.ForeColor = Theme.Text; }
        b.Click += (_, _) => click();
        return b;
    }

    static Label L(Font? f = null, Color? c = null) => new()
    {
        AutoSize = true, Font = f, ForeColor = c ?? Theme.Text, Margin = new Padding(0, 4, 0, 4), BackColor = Color.Transparent,
    };

    /// <summary>Bölüm başlığı (Falcon .card-title: küçük, kalın, label rengi).</summary>
    Label Section(Font f)
    {
        var l = L(f, Theme.Label);
        l.Margin = new Padding(0, D(16), 0, D(2));
        return l;
    }

    Panel Card(Control inner)
    {
        var p = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(D(20)), Margin = new Padding(D(8)) };
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        return p;
    }

    FlowLayoutPanel Flow()
    {
        var f = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Card,
        };
        Theme.DarkScroll(f);
        f.SizeChanged += (_, _) =>
        {
            foreach (Control c in f.Controls)
                if (c is Label or RadioButton or CheckBox or FlowLayoutPanel)
                    c.MaximumSize = new Size(Math.Max(100, f.ClientSize.Width - D(24)), 0);
        };
        return f;
    }

    void BuildUI()
    {
        var hFont = Theme.F(13f, true);
        var sFont = Theme.F(10f, true);

        // ---- üst çubuk: sağ üstte (soldan sağa) 🔔 · Eşitle · Güncelle · tema · dil | Küçült · Orta · Tam ekran · Kapat ----
        titleBar = new Panel { Dock = DockStyle.Top, Height = D(46), BackColor = Theme.TitleBg };
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.TitleBg,
            Margin = Padding.Empty, Padding = Padding.Empty,
        };
        bClose = WinButton(Theme.GClose, () => Close(), danger: true);
        bFull = WinButton(Theme.GFull, ToggleFull);
        bHalf = WinButton(Theme.GRestore, ToggleHalf);
        bMin = WinButton(Theme.GMin, () => WindowState = FormWindowState.Minimized);
        var sep = new Panel { Width = Math.Max(1, D(1)), Height = D(22), Margin = new Padding(D(6), D(12), D(6), 0), BackColor = Look.Tok("border-strong", Theme.TitleBg) };
        bLang = WinButton(LangCode(), ShowLangMenu);
        bLang.Font = Theme.F(9.5f, true);
        bTheme = WinButton(Look.IsDark ? Theme.GMoon : Theme.GSun, ToggleTheme);
        bSync = WinButton(Theme.GSync, async () => await SyncClicked());
        bUpdate = WinButton(Theme.GDownload, UpdateClicked);
        bBell = WinButton("🔔", () => { });
        bAgentTop = WinButton("✦", () => agent?.ShowPanel(this));
        bAgentTop.AutoSize = true;
        bAgentTop.Tag = "fcAgent";
        bBell.Font = new Font("Segoe UI Emoji", 10f);
        bBell.AutoSize = true;
        btns.Controls.AddRange(new Control[] { bClose, bFull, bHalf, bMin, sep, bLang, bTheme, bUpdate, bSync, bBell, bAgentTop });
        if (announcements == null) announcements = new Announcements(this, bBell, UpdateClicked);
        else announcements.Rebind(bBell);

        logo = new PictureBox
        {
            Image = Icon!.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Width = D(26), Height = D(26),
            Left = D(14), Top = D(10), BackColor = Theme.TitleBg,
        };
        titleLbl = new Label
        {
            AutoSize = true, Left = D(48), Top = D(12), Font = Theme.F(11f, true),
            ForeColor = Theme.Text, BackColor = Theme.TitleBg,
        };
        titleBar.Controls.Add(btns);
        titleBar.Controls.Add(logo);
        titleBar.Controls.Add(titleLbl);
        titleBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Look.Tok("border", Theme.TitleBg) }); // son eklenen önce yerleşir: tam genişlik
        titleBar.MouseDown += DragWindow;
        titleLbl.MouseDown += DragWindow;
        logo.MouseDown += DragWindow;

        // ---- content grid ----
        content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(D(12)), BackColor = Theme.Bg,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        // status card
        var fs = Flow();
        hStatus = L(hFont);
        lStatus = L(Theme.F(12f, true));
        lHow = L(Theme.F(10f), Theme.Sub);
        lScreensH = Section(sFont);
        lScreensH.Margin = new Padding(0, D(10), 0, D(2));
        lScreens = L();
        lProfile = L(Theme.F(9.5f, true));
        // Windows "otomatik düzenle / kılavuza hizala" uyarısı (yalnızca açıkken görünür)
        arrRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = new Padding(0, D(6), 0, D(2)), Visible = false };
        lArr = L(Theme.F(9.5f), Theme.Warn);
        bArrFix = Theme.ActionButton("", Btn.Secondary);
        bArrFix.Click += (_, _) => guard.TurnOffArrangeFlags();
        arrRow.Controls.AddRange(new Control[] { lArr, bArrFix });
        lIcons = L();
        lSave = L();
        lRestore = L();
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = new Padding(0, D(8), 0, 0) };
        bProtect = Theme.ActionButton("", Btn.Secondary);
        bSaveNow = Theme.ActionButton("", Btn.Primary);
        bRestore = Theme.ActionButton("", Btn.Ok);
        bProtect.Click += (_, _) => ToggleProtect();
        bSaveNow.Click += (_, _) => guard.SaveNow("manual");
        bRestore.Click += (_, _) => guard.RestoreNow();
        bPreviewNow = Theme.ActionButton("", Btn.Accent2);
        bPreviewNow.Click += (_, _) => OpenPreview(null);
        actions.Controls.AddRange(new Control[] { bSaveNow, bRestore, bPreviewNow, bProtect });
        hLog = Section(sFont);
        lb = new ListBox
        {
            BackColor = Theme.Input, ForeColor = Theme.Sub, BorderStyle = BorderStyle.None,
            IntegralHeight = false, Margin = new Padding(0, D(4), 0, 0), Font = Theme.F(9.5f),
            HorizontalScrollbar = true,
        };
        Theme.DarkScroll(lb);
        statusFlow = fs;
        fs.Controls.AddRange(new Control[] { hStatus, lStatus, lHow, lScreensH, lScreens, lProfile, arrRow, lIcons, lSave, lRestore, actions, hLog, lb });
        fs.SizeChanged += (_, _) =>
        {
            int mw = Math.Max(100, fs.ClientSize.Width - D(12));
            actions.MaximumSize = new Size(mw, 0);
            arrRow.MaximumSize = new Size(mw, 0);
            lArr.MaximumSize = new Size(Math.Max(100, mw - D(10)), 0);
            lProfile.MaximumSize = new Size(mw, 0);
        };
        // the activity list takes whatever room is left at the bottom of the status card
        fs.Layout += (_, _) =>
        {
            int w = Math.Max(D(100), fs.ClientSize.Width - D(12));
            int h = Math.Max(D(90), fs.ClientSize.Height - lb.Top - D(4));
            if (lb.Width != w || lb.Height != h) lb.Size = new Size(w, h);
        };

        // settings card — Ayarlar: koruma · Görünüm · Arşiv · Kullanım Limitleri · Otomatik hata raporu · Hakkında · Asistan Agent · Admin ile iletişim (EN SON)
        var fset = Flow();
        settingsFlow = fset;
        hSettings = L(hFont);
        lMode = Section(sFont);
        lMode.Margin = new Padding(0, D(8), 0, D(2));
        lModeInfo = L(Theme.F(9.5f), Theme.Text);
        lModeInfo.Margin = new Padding(0, 4, 0, 10);
        cAuto = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4), FlatStyle = FlatStyle.Flat };
        cStartup = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4), FlatStyle = FlatStyle.Flat };
        cAutoSave = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4), FlatStyle = FlatStyle.Flat };
        lSaveInfo = L(Theme.F(9f), Theme.Sub);
        lSaveInfo.Margin = new Padding(0, 2, 0, 10);
        cAuto.Checked = settings.DisableAutoArrange;
        cStartup.Checked = settings.StartWithWindows;
        cAutoSave.Checked = settings.AutoSave;
        cAutoSave.CheckedChanged += (_, _) => { if (!loading) { settings.AutoSave = cAutoSave.Checked; Storage.SaveSettings(settings); } };
        cAuto.CheckedChanged += (_, _) => { if (!loading) { settings.DisableAutoArrange = cAuto.Checked; Storage.SaveSettings(settings); } };
        cStartup.CheckedChanged += (_, _) =>
        {
            if (loading) return;
            settings.StartWithWindows = cStartup.Checked;
            Storage.SaveSettings(settings);
            Installer.SetStartup(settings.StartWithWindows);
        };
        fset.Controls.AddRange(new Control[] { hSettings, lMode, lModeInfo, cAuto, cAutoSave, cStartup, lSaveInfo });

        // Görünüm: şema · tema · yazı boyutu · dil
        hLook = Section(Theme.F(11.5f, true));
        lLookDesc = L(Theme.F(9f), Theme.Sub);
        fset.Controls.Add(hLook);
        fset.Controls.Add(lLookDesc);
        OnboardingForm.AddLookControls(fset, D(520), SetLook);
        lLang = Section(sFont);
        lLang.Margin = new Padding(0, D(10), 0, D(2));
        cLang = Theme.Combo();
        cLang.Width = D(220);
        foreach (var (_, name) in Lang.Languages) cLang.Items.Add(name);
        cLang.SelectedIndex = Math.Max(0, Array.FindIndex(Lang.Languages, x => x.code == settings.Lang));
        cLang.SelectedIndexChanged += (_, _) =>
        {
            if (loading || cLang.SelectedIndex < 0) return;
            BeginInvoke(() => SetLang(Lang.Languages[cLang.SelectedIndex].code));
        };
        fset.Controls.Add(lLang);
        fset.Controls.Add(cLang);

        // Arşiv
        bArchive = Theme.ActionButton("", Btn.Secondary);
        bArchive.Margin = new Padding(0, D(16), 0, 0);
        bArchive.Click += (_, _) =>
        {
            using var f = new ArchiveForm(store);
            f.ShowDialog(this);
            if (f.Changed) RefreshProfiles();
        };
        fset.Controls.Add(bArchive);

        // Kullanım Limitleri
        lUsageH = Section(sFont);
        lUsage = L(Theme.F(9f), Theme.Sub);
        lUsage.Margin = new Padding(0, 2, 0, 4);
        fset.Controls.Add(lUsageH);
        fset.Controls.Add(lUsage);

        // Otomatik hata raporu
        hCrash = Section(sFont);
        cCrash = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 2), FlatStyle = FlatStyle.Flat, Checked = shot || Crash.Enabled };
        cCrash.CheckedChanged += (_, _) => { if (!loading) { Crash.Enabled = cCrash.Checked; UpdateCrashInfo(); } };
        lCrash = L(Theme.F(9f), Theme.Sub);
        fset.Controls.AddRange(new Control[] { hCrash, cCrash, lCrash });

        // Hakkında
        hAbout = Section(sFont);
        lAbout = L(Theme.F(9.5f), Theme.Text);
        var aboutRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = new Padding(0, 0, 0, 0) };
        bCheckUpd = Theme.ActionButton("", Btn.Secondary);
        bCheckUpd.Click += (_, _) => UpdateClicked();
        bWhatsNew = Theme.ActionButton("", Btn.Secondary);
        bWhatsNew.Click += (_, _) => ShowWhatsNew();
        bOnboard = Theme.ActionButton("", Btn.Secondary);
        bOnboard.Click += (_, _) => ShowOnboarding();
        aboutRow.Controls.AddRange(new Control[] { bCheckUpd, bWhatsNew, bOnboard });
        fset.Controls.AddRange(new Control[] { hAbout, lAbout, aboutRow });

        bAgent = Theme.ActionButton("", Btn.Secondary);
        bAgent.Margin = new Padding(0, D(16), 0, 0);
        bAgent.Tag = "fcAgent";
        bAgent.Click += (_, _) => { agent?.ShowSettings(this); UpdateAgentButton(); };
        fset.Controls.Add(bAgent);

        // Admin ile iletişim — Ayarlar'ın EN SONUNDA
        bContact = Theme.ActionButton("", Btn.Primary);
        bContact.Margin = new Padding(0, D(16), 0, D(8));
        bContact.Click += (_, _) => { using var f = new ContactForm(); f.ShowDialog(this); };
        fset.Controls.Add(bContact);

        // profiles card
        var tp = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, BackColor = Theme.Card };
        tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        hProfiles = L(hFont);
        lv = new ListView
        {
            View = View.Details, FullRowSelect = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
            OwnerDraw = true, Margin = new Padding(0, D(6), 0, D(6)), Font = Theme.F(9.5f),
        };
        // her satırda kaydın küçük önizlemesi: satır yüksekliği bir ImageList ile büyütülür
        lv.SmallImageList = new ImageList { ImageSize = new Size(1, D(46)) };
        lvMenu = new ContextMenuStrip();
        miRename = new ToolStripMenuItem("", null, (_, _) => RenameSelected());
        lvMenu.Items.Add(miRename);
        lvMenu.Opening += (_, e) => { if (SelectedSnap() == null) e.Cancel = true; };
        lv.ContextMenuStrip = lvMenu;
        lv.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && lv.GetItemAt(e.X, e.Y) is ListViewItem hit) hit.Selected = true;
        };
        lv.Columns.Add("", 300);
        lv.Columns.Add("", 60);
        lv.Columns.Add("", 140);
        lv.Columns.Add("", 120);
        lv.MultiSelect = false;
        lv.DoubleClick += (_, _) => OpenPreview(SelectedSnap());
        lv.SelectedIndexChanged += (_, _) => UpdatePreview();
        lv.DrawColumnHeader += (_, e) =>
        {
            using var bg = new SolidBrush(Theme.Card);
            e.Graphics.FillRectangle(bg, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, lv.Font, e.Bounds, Theme.Label,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                (Lang.IsRtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0));
        };
        lv.DrawItem += (_, _) => { };
        lv.DrawSubItem += (_, e) =>
        {
            if (e.Item == null) return;
            var bg = e.Item.Selected ? Look.Tok("active-1", Theme.Input) : Theme.Input;
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var a = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(a, Lang.IsRtl ? e.Bounds.Right - D(3) : e.Bounds.Left, e.Bounds.Top, D(3), e.Bounds.Height);
            var r = e.Bounds; r.Inflate(-D(6), 0);
            if (e.ColumnIndex == 0 && e.Item.Tag is string sid && store.Find(sid) is { } fs0)
            {
                int tw = D(64), th = Math.Max(D(20), e.Bounds.Height - D(8));
                var tr = new Rectangle(Lang.IsRtl ? r.Right - tw : r.Left, e.Bounds.Top + (e.Bounds.Height - th) / 2, tw, th);
                LayoutPreview.DrawMini(e.Graphics, tr, fs0.p, fs0.s, fs0.p.Key == guard?.CurrentKey);
                if (Lang.IsRtl) r.Width -= tw + D(8); else { r.X += tw + D(8); r.Width -= tw + D(8); }
            }
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, lv.Font, r, e.Item.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine |
                (Lang.IsRtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
        };
        Theme.DarkScroll(lv);
        lv.Resize += (_, _) => FitColumns();
        bDel = Theme.ActionButton("", Btn.Danger);
        bDel.Click += (_, _) => DeleteSelected();
        bRestoreSel = Theme.ActionButton("", Btn.Ok);
        bRestoreSel.Click += (_, _) => RestoreSelected();
        bPreviewSel = Theme.ActionButton("", Btn.Accent2);
        bPreviewSel.Click += (_, _) => OpenPreview(SelectedSnap());
        var hb = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = Padding.Empty };
        hb.Controls.AddRange(new Control[] { bPreviewSel, bRestoreSel, bDel });
        tp.Controls.Add(hProfiles, 0, 0);
        tp.Controls.Add(lv, 0, 1);
        tp.Controls.Add(hb, 0, 2);

        // preview card: shows the record selected in the history list (one click is enough)
        var tl = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, BackColor = Theme.Card };
        tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hPreview = L(hFont);
        lPvInfo = L(Theme.F(9f), Theme.Sub);
        lPvInfo.AutoEllipsis = true;
        preview = new LayoutPreview(store, () => guard?.CurrentKey ?? "")
        {
            Dock = DockStyle.Fill, Compact = true, Margin = new Padding(0, D(6), 0, 0), Cursor = Cursors.Hand,
        };
        preview.DoubleClick += (_, _) => OpenPreview(preview.SnapId);
        tl.SizeChanged += (_, _) => lPvInfo.MaximumSize = new Size(Math.Max(100, tl.ClientSize.Width - D(8)), 0);
        tl.Controls.Add(hPreview, 0, 0);
        tl.Controls.Add(lPvInfo, 0, 1);
        tl.Controls.Add(preview, 0, 2);

        content.Controls.Add(Card(fs), 0, 0);
        content.Controls.Add(Card(fset), 1, 0);
        content.Controls.Add(Card(tp), 0, 1);
        content.Controls.Add(Card(tl), 1, 1);

        Controls.Add(content);
        Controls.Add(titleBar);

        // tray menu
        trayMenu = new ContextMenuStrip();
        miOpen = new ToolStripMenuItem("", null, (_, _) => ShowMain());
        miSave = new ToolStripMenuItem("", null, (_, _) => guard.SaveNow("manual"));
        miRestore = new ToolStripMenuItem("", null, (_, _) => guard.RestoreNow());
        miExit = new ToolStripMenuItem("", null, (_, _) => ExitApp());
        trayMenu.Items.AddRange(new ToolStripItem[] { miOpen, new ToolStripSeparator(), miSave, miRestore, new ToolStripSeparator(), miExit });
    }

    void FitColumns()
    {
        if (lv.Columns.Count < 4) return;
        int w = lv.ClientSize.Width;
        lv.Columns[1].Width = D(60);
        lv.Columns[2].Width = D(130);
        lv.Columns[3].Width = D(150);
        lv.Columns[0].Width = Math.Max(D(200), w - D(60) - D(130) - D(150) - 4);
    }

    // ---------------- texts ----------------

    void ApplyLanguage()
    {
        Lang.Current = settings.Lang;
        bool rtl = Lang.IsRtl;
        CoreStrings.Lang = () => Lang.Current;
        content.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        trayMenu.RightToLeft = content.RightToLeft;

        Text = $"{Lang.T("app_title")} v{Updater.CurrentText}";
        titleLbl.Text = Text;
        hStatus.Text = Lang.T("status_hdr");
        lHow.Text = Lang.T("how_it_works");
        lScreensH.Text = Lang.T("screens");
        bSaveNow.Text = Lang.T("save_now");
        bRestore.Text = Lang.T("restore_now");
        bPreviewNow.Text = "🖥  " + Lang.T("preview_now");
        hSettings.Text = Lang.T("settings_hdr");
        lMode.Text = Lang.T("mode");
        lModeInfo.Text = Lang.T("mode_info");
        cAuto.Text = Lang.T("autoarr");
        cStartup.Text = Lang.T("startup");
        cAutoSave.Text = Lang.T("autosave");
        lSaveInfo.Text = Lang.T("save_info");
        lLang.Text = CoreStrings.T("fc.lang");
        hLook.Text = CoreStrings.T("fc.look");
        lLookDesc.Text = CoreStrings.T("fc.look.desc");
        bArchive.Text = "🗄  " + Lang.T("archive_btn") + (store.Archive.Count(a => !a.Restored) is int na && na > 0 ? $"  ({na})" : "");
        bContact.Text = "✉  " + CoreStrings.T("fc.contact.title");
        UpdateAgentButton();
        lUsageH.Text = CoreStrings.T("fc.usage.title");
        UpdateUsage();
        hCrash.Text = CoreStrings.T("fc.crash.title");
        cCrash.Text = CoreStrings.T("fc.crash.on");
        UpdateCrashInfo();
        hAbout.Text = CoreStrings.T("fc.settings.about");
        lAbout.Text = $"{Lang.T("app_title")} · {CoreStrings.T("fc.settings.version")} {Updater.CurrentText}";
        bCheckUpd.Text = CoreStrings.T("fc.update.check");
        bWhatsNew.Text = CoreStrings.T("fc.wn.show");
        bOnboard.Text = CoreStrings.T("fc.ob.reopen");
        hProfiles.Text = Lang.T("profiles_hdr");
        lv.Columns[0].Text = Lang.T("col_display");
        lv.Columns[1].Text = Lang.T("col_icons");
        lv.Columns[2].Text = Lang.T("col_date");
        lv.Columns[3].Text = Lang.T("col_src");
        bRestoreSel.Text = Lang.T("restore_sel");
        bPreviewSel.Text = "🖥  " + Lang.T("preview_sel");
        tips.SetToolTip(bPreviewSel, Lang.T("tt_preview"));
        lv.RightToLeftLayout = rtl;
        bDel.Text = Lang.T("delete");
        miRename.Text = Lang.T("rename");
        bArrFix.Text = Lang.T("arr_fix");
        hLog.Text = Lang.T("log_hdr");
        hPreview.Text = Lang.T("pv_title");
        preview.EmptyText = Lang.T("log_noLayout");
        tips.SetToolTip(preview, Lang.T("pv_side_tt"));
        miOpen.Text = Lang.T("tray_open");
        miSave.Text = Lang.T("save_now");
        miRestore.Text = Lang.T("restore_now");
        miExit.Text = Lang.T("tray_exit");

        tips.SetToolTip(bSync, Lang.T("tt_sync"));
        tips.SetToolTip(bMin, CoreStrings.T("fc.win.minimize"));
        tips.SetToolTip(bClose, Lang.T("tt_close"));
        tips.SetToolTip(bTheme, CoreStrings.T("fc.theme.toggle"));
        tips.SetToolTip(bLang, CoreStrings.T("fc.lang"));
        tips.SetToolTip(bDel, Lang.T("tt_delete"));
        UpdateHalfGlyph();
        UpdateUpdateButton();

        lb.Items.Clear();
        foreach (var e in logs) lb.Items.Add(FormatLog(e));
        RefreshInfo();
        // re-run wrapping with the new texts
        foreach (Control card in content.Controls)
            foreach (Control inner in card.Controls)
                if (inner is FlowLayoutPanel f) f.PerformLayout();
        if (IsHandleCreated) BeginInvoke(() => { statusFlow.AutoScrollPosition = Point.Empty; settingsFlow.AutoScrollPosition = Point.Empty; });
    }

    static string When(DateTime? t) =>
        t == null ? Lang.T("never") : t.Value.Date == DateTime.Today ? t.Value.ToString("HH:mm:ss") : t.Value.ToString("dd.MM.yyyy HH:mm");

    void RefreshInfo()
    {
        if (guard == null) return;
        bool on = settings.Protect;
        lStatus.Text = Lang.T(on ? "prot_on" : "prot_off");
        lStatus.ForeColor = on ? Theme.GreenText : Theme.RedText;
        bProtect.Text = Lang.T(on ? "btn_prot_off" : "btn_prot_on");

        var mons = guard.Monitors.Count > 0 ? guard.Monitors : DisplayInfo.GetMonitors();
        lScreens.Text = string.Join("\n", mons.Select((m, i) =>
            $"{i + 1})  {m.Bounds.Width}×{m.Bounds.Height}   %{m.Dpi * 100 / 96}   {m.Model}{(m.Primary ? "   — " + Lang.T("primary") : "")}"));
        lIcons.Text = Lang.F("icons", guard.IconCount);
        var srcInfo = guard.DescribeSource();
        int nRec = store.Displays.TryGetValue(guard.CurrentKey, out var curP) ? curP.History.Count : 0;
        if (srcInfo == null) { lProfile.Text = "▸ " + Lang.T("profile_none"); lProfile.ForeColor = Theme.Sub; }
        else if (srcInfo.Value.kind == SourceKind.Exact) { lProfile.Text = "✔ " + Lang.F("profile_cur", srcInfo.Value.p.Title, nRec); lProfile.ForeColor = Theme.GreenText; }
        else if (srcInfo.Value.kind == SourceKind.SameScreens) { lProfile.Text = "↻ " + Lang.F("profile_same", srcInfo.Value.p.Title); lProfile.ForeColor = Theme.Text; }
        else { lProfile.Text = "⚠ " + Lang.F("profile_new", srcInfo.Value.p.Title); lProfile.ForeColor = Theme.Warn; }
        arrRow.Visible = guard.AutoArrangeOn || guard.SnapToGridOn;
        lArr.Text = guard.AutoArrangeOn ? "⚠ " + Lang.T("arr_auto") : guard.SnapToGridOn ? "ℹ " + Lang.T("arr_snap") : "";
        lSave.Text = Lang.F("last_save", When(guard.LastSave));
        lRestore.Text = Lang.F("last_restore", When(guard.LastRestore));
        tray.Text = Lang.T(on ? "prot_on" : "prot_off") is var s && s.Length > 60 ? s[..60] : s;
        RefreshProfiles();
    }

    void RefreshProfiles()
    {
        string? sel = lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as string : null;
        lv.BeginUpdate();
        lv.Items.Clear();
        var profiles = store.Displays.Values
            .OrderByDescending(p => p.Key == guard.CurrentKey)
            .ThenByDescending(p => p.Latest?.SavedUtc ?? DateTime.MinValue);
        foreach (var p in profiles)
        {
            bool cur = p.Key == guard.CurrentKey;
            string name = p.Title;
            for (int i = 0; i < p.History.Count; i++)
            {
                var h = p.History[i];
                bool top = i == 0;
                var it = new ListViewItem(name + (cur && top ? $"   ◀ {Lang.T("current")}" : ""))
                {
                    Tag = h.Id,
                    ForeColor = cur && top ? Look.Tok("ok-text", Theme.Input) : top ? Theme.Text : Theme.Sub,
                };
                it.SubItems.Add(h.Icons.Count.ToString());
                it.SubItems.Add(h.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
                it.SubItems.Add(Lang.T("src_" + h.Source));
                if (h.Id == sel) it.Selected = true;
                lv.Items.Add(it);
            }
        }
        if (lv.SelectedItems.Count == 0 && lv.Items.Count > 0) lv.Items[0].Selected = true;
        lv.EndUpdate();
        FitColumns();
        UpdatePreview();
    }

    /// <summary>Shows the selected record (or the newest one of the current screen) in the side preview.</summary>
    void UpdatePreview()
    {
        if (preview == null) return;
        string? id = SelectedSnap();
        if (id == null && guard != null && store.Displays.TryGetValue(guard.CurrentKey, out var cp)) id = cp.Latest?.Id;
        preview.SnapId = id;
        if (id == null || store.Find(id) is not { } f) { lPvInfo.Text = ""; return; }
        var (p, s) = f;
        bool cur = guard != null && p.Key == guard.CurrentKey;
        string name = p.Title;
        lPvInfo.Text = (cur ? "✔ " : "⚠ ") + name + "   ·   " + s.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm") +
            "   ·   " + Lang.F("pv_icons_on", s.Icons.Count) + "   ·   " + Lang.T("src_" + s.Source) +
            (cur ? "" : "   ·   " + Lang.T("pv_other_short"));
        lPvInfo.ForeColor = cur ? Theme.Sub : Theme.Warn;
    }

    string? SelectedSnap() => lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as string : null;

    void RestoreSelected()
    {
        if (SelectedSnap() is string id) guard.RestoreSnapshot(id);
    }

    /// <summary>Opens the big screen preview for a record (null = the newest record of the screen in use now).</summary>
    void OpenPreview(string? snapId)
    {
        var ids = lv.Items.Cast<ListViewItem>().Select(i => i.Tag as string).OfType<string>().ToList();
        if (ids.Count == 0) { OnLog("log_noLayout", Array.Empty<object>()); return; }
        snapId ??= store.Displays.TryGetValue(guard.CurrentKey, out var cp) && cp.Latest != null ? cp.Latest.Id : ids[0];
        int idx = Math.Max(0, ids.IndexOf(snapId));
        using var f = new PreviewForm(store, ids, idx, () => guard.CurrentKey, id => guard.RestoreSnapshot(id));
        f.ShowDialog(this);
    }

    /// <summary>Seçili kaydın ekran düzenine ad verir ("Ofis masası" gibi). Boş bırakılırsa otomatik ad kullanılır.</summary>
    void RenameSelected()
    {
        if (SelectedSnap() is not string id || store.Find(id) is not { } f) return;
        using var dlg = new NameForm(f.p.Name ?? "", f.p.AutoLabel() ?? f.p.Display);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var name = dlg.Value.Trim();
        f.p.Name = name.Length == 0 ? null : name;
        f.p.NameUtc = DateTime.UtcNow;
        Storage.SaveStore(store);
        RefreshInfo();
    }

    /// <summary>Kullanılmayan belleği Windows'a geri verir (tepside beklerken birkaç MB).</summary>
    static void TrimMemory()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            Win32.SetProcessWorkingSetSize(Win32.GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
    }

    void DeleteSelected()
    {
        // v1.1.0: kalıcı silme yok — kayıt Arşiv'e taşınır (Ayarlar → Arşiv'den geri alınır)
        if (SelectedSnap() is not string id || !store.ArchiveSnapshot(id)) return;
        Storage.SaveStore(store);
        RefreshProfiles();
        bArchive.Text = "🗄  " + Lang.T("archive_btn") + $"  ({store.Archive.Count(a => !a.Restored)})";
    }

    void ToggleProtect()
    {
        settings.Protect = !settings.Protect;
        Storage.SaveSettings(settings);
        if (settings.Protect) guard.Resume();
        RefreshInfo();
    }

    string FormatLog((DateTime t, string key, object[] args) e) => $"{e.t:HH:mm:ss}   {Lang.F(e.key, e.args)}";

    void OnLog(string key, object[] args)
    {
        var e = (DateTime.Now, key, args);
        logs.Insert(0, e);
        if (logs.Count > 300) logs.RemoveAt(logs.Count - 1);
        lb.Items.Insert(0, FormatLog(e));
        while (lb.Items.Count > 300) lb.Items.RemoveAt(lb.Items.Count - 1);
    }

    // ---------------- updates ----------------

    void UpdateAgentButton()
    {
        if (bAgent == null || bAgentTop == null) return;
        bAgent.Text = "✦ " + (agent?.T("title") ?? Lang.T("agent_title"));
        bAgentTop.Text = agent?.ButtonText ?? "✦";
        bAgentTop.Visible = agent != null && agent.On && agent.Panel;
    }

    void UpdateUpdateButton()
    {
        if (update != null)
        {
            bUpdate.ForeColor = Look.Tok("ok-text", Theme.TitleBg);
            bUpdate.BackColor = Look.Tok("ok-soft", Theme.TitleBg);
            tips.SetToolTip(bUpdate, CoreStrings.T1("fc.update.avail", "v", update.Version.ToString(3)) + " — " + CoreStrings.T("fc.update.press"));
        }
        else
        {
            bUpdate.ForeColor = Theme.Text;
            bUpdate.BackColor = Theme.TitleBg;
            tips.SetToolTip(bUpdate, CoreStrings.T("fc.update"));
        }
    }

    async Task CheckUpdateSilently()
    {
        try
        {
            var u = await Updater.CheckAsync();
            if (u != null && (update == null || u.Version > update.Version))
            {
                update = u;
                OnLog("log_update", new object[] { u.Version.ToString(3) });
                UpdateUpdateButton();
                // 1.1.2: otomatik güncelleme — sorulmadan arka planda indirilir, doğrulanır ve sessiz kurulur;
                // program birkaç saniyede kendiliğinden yeniden açılır (kayıtlar önce kaydedilir). Hata olursa düğme/balon kalır.
                if (!shot && await AutoInstall(u)) return;
                tray.ShowBalloonTip(4000, Lang.T("app_title"), Lang.F("tt_update_new", u.Version.ToString(3)), ToolTipIcon.Info);
            }
        }
        catch { }
    }

    bool autoInstalling;
    async Task<bool> AutoInstall(UpdateInfo u)
    {
        if (autoInstalling) return false;
        autoInstalling = true;
        try
        {
            var file = await Updater.DownloadAsync(u, (_, _) => { }, CancellationToken.None);
            tray.ShowBalloonTip(3000, Lang.T("app_title"), CoreStrings.T("fc.update.installing") + " v" + u.Version.ToString(3), ToolTipIcon.Info);
            Updater.ApplyAndRestart(file, u);
            await Task.Delay(800);
            ExitApp();
            return true;
        }
        catch { autoInstalling = false; return false; }
    }

    void UpdateClicked()
    {
        if (shot) return;
        using var f = new UpdateForm(update);
        if (f.ShowDialog(this) == DialogResult.OK && f.Installed) ExitApp();
    }

    // ---------------- GitHub sync ----------------

    async Task SyncClicked()
    {
        if (syncing) return;
        if (GitHubSync.GetToken(settings) == null)
        {
            using var tf = new TokenForm();
            if (tf.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(tf.Token)) return;
            GitHubSync.SetToken(settings, tf.Token);
            Storage.SaveSettings(settings);
        }
        if (guard.Trusted) guard.SaveNow("manual");
        var err = await DoSync();
        if (err == null) MessageBox.Show(this, Lang.T("sync_ok"), Lang.T("sync_title"), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, Lang.IsRtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);
        else MessageBox.Show(this, Lang.F("sync_fail", err), Lang.T("sync_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, Lang.IsRtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);
    }

    async Task AutoSync()
    {
        if (syncing || GitHubSync.GetToken(settings) == null || store.Displays.Count == 0) return;
        if (lastSyncedSave == store.LatestUtc) return;
        await DoSync();
    }

    async Task<string?> DoSync()
    {
        syncing = true;
        bSync.Enabled = false;
        try
        {
            var merged = await GitHubSync.SyncAsync(settings, store);
            store.Displays = merged.Displays;
            store.Archive = merged.Archive;
            Storage.SaveStore(store);
            Storage.SaveSettings(settings);
            lastSyncedSave = store.LatestUtc;
            OnLog("log_synced", Array.Empty<object>());
            RefreshInfo();
            return null;
        }
        catch (Exception ex)
        {
            if (ex.Message.StartsWith("token"))
            {
                settings.TokenEnc = null;
                settings.GistId = null;
                Storage.SaveSettings(settings);
            }
            OnLog("log_err", new object[] { ex.Message });
            return ex.Message;
        }
        finally
        {
            syncing = false;
            bSync.Enabled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tray.Dispose();
            showEvent.Dispose();
            updateTimer.Dispose();
            autoSyncTimer.Dispose();
            usageTimer.Dispose();
            inboxTimer.Dispose();
            quitEvent?.Dispose();
        }
        base.Dispose(disposing);
    }

    // Ayarlar > Kullanım Limitleri — bulut/AI yok; yalnız kullanıcının kendi GitHub hesabıyla gist eşitlemesi (saatlik istek sınırı)
    void UpdateUsage()
    {
        if (lUsage == null) return;
        bool linked = !shot && GitHubSync.GetToken(settings) != null;
        var t = linked ? Lang.T("usage_none") : CoreStrings.T("fc.usage.none") + Environment.NewLine + Lang.T("usage_sync_off");
        if (GitHubSync.RateRemaining is int rem && GitHubSync.RateLimit is int lim)
            t += Environment.NewLine + string.Format(Lang.T("usage_gh"), rem, lim, GitHubSync.RateReset?.ToString("HH:mm") ?? "—");
        lUsage.Text = t;
    }

    void UpdateCrashInfo()
    {
        if (lCrash == null) return;
        lCrash.Text = CoreStrings.T("fc.crash.desc") + Environment.NewLine + CoreStrings.T1("fc.crash.sent", "n", shot ? 0 : Crash.SentToday());
    }

    // ---------------- görünüm (ortak çekirdek) ----------------

    static bool SystemLight()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return k?.GetValue("AppsUseLightTheme") is int v && v == 1;
    }

    void OnSystemPrefs(object? s, UserPreferenceChangedEventArgs e)
    {
        if (Look.Mode != "auto" || e.Category != UserPreferenceCategory.General) return;
        try { BeginInvoke(RebuildUI); } catch { }
    }

    void StyleForm()
    {
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.F(10f);
    }

    string LangCode() => Lang.Current switch { "en" => "EN", "ar" => "ع", _ => "TR" };

    void ShowLangMenu()
    {
        var m = new ContextMenuStrip { RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No, Font = Theme.F(10f) };
        foreach (var (code, name) in Lang.Languages)
            m.Items.Add(new ToolStripMenuItem(name, null, (_, _) => BeginInvoke(() => SetLang(code))) { Checked = code == Lang.Current });
        m.Closed += (_, _) => BeginInvoke(m.Dispose);
        m.Show(bLang, new Point(0, bLang.Height));
    }

    void ToggleTheme()
    {
        var next = Look.Mode == "auto" ? (Look.IsDark ? "light" : "dark") : Look.Mode == "dark" ? "light" : "dark";
        SetLook(null, next, null);
    }

    void SetLang(string code)
    {
        if (code == settings.Lang) return;
        settings.Lang = code;
        Lang.Current = code;
        Storage.SaveSettings(settings);
        RebuildUI();
    }

    void SetLook(string? scheme, string? mode, double? scale)
    {
        Look.Set(scheme, mode, scale, notify: false);
        settings.Scheme = Look.Scheme;
        settings.Theme = Look.Mode;
        settings.FontScale = Look.Scale;
        Storage.SaveSettings(settings);
        BeginInvoke(RebuildUI);
    }

    /// <summary>Şema / tema / yazı boyutu / dil değişince pencere içeriği yeniden kurulur (WinForms'ta en güvenilir yol).</summary>
    void RebuildUI()
    {
        loading = true;
        SuspendLayout();
        try
        {
            var old = Controls.Cast<Control>().ToList();
            Controls.Clear();
            var oldMenu = trayMenu;
            Lang.Current = settings.Lang;
            StyleForm();
            BuildUI();
            tray.ContextMenuStrip = trayMenu;
            foreach (var c in old) c.Dispose();
            oldMenu?.Dispose();
            ApplyLanguage();
            UpdateHalfGlyph();
        }
        finally
        {
            ResumeLayout(true);
            loading = false;
        }
        Theme.DarkTitle(this);
        Invalidate(true);
    }

    // ---------------- ilk açılış rehberi · neler yeni ----------------

    /// <summary>İlk kez: rehber (dil + görünüm). Güncellemeden sonraki ilk açılış: "neler yeni" (bir kez).</summary>
    void Intro()
    {
        if (introDone || shot) return;
        introDone = true;
        var v = Updater.CurrentText;
        if (!settings.Onboarded)
        {
            settings.Onboarded = true;
            bool fresh = !Storage.SettingsExisted;
            var seen = settings.WhatsNewSeen;
            settings.WhatsNewSeen = v;
            Storage.SaveSettings(settings);
            if (fresh) ShowOnboarding();
            else ShowWhatsNew();    // 1.0.x'ten gelen: yeni görünümü ve Arşiv'i anlat
            return;
        }
        var last = settings.WhatsNewSeen;
        settings.WhatsNewSeen = v;
        Storage.SaveSettings(settings);
        if (last != null && Version.TryParse(last, out var lastV) && Version.TryParse(v, out var curV) && curV > lastV) ShowWhatsNew();
    }

    void ShowOnboarding()
    {
        using var f = new OnboardingForm((lang, scheme, mode, scale) =>
        {
            if (lang != null && lang != settings.Lang) { settings.Lang = lang; Lang.Current = lang; Storage.SaveSettings(settings); }
            if (scheme != null || mode != null || scale != null)
            {
                Look.Set(scheme, mode, scale, notify: false);
                settings.Scheme = Look.Scheme; settings.Theme = Look.Mode; settings.FontScale = Look.Scale;
                Storage.SaveSettings(settings);
            }
            RebuildUI();
        });
        f.ShowDialog(this);
    }

    void ShowWhatsNew()
    {
        var v = Updater.CurrentText;
        using var f = new WhatsNewForm(v, WhatsNewForm.NotesFor(v));
        f.ShowDialog(this);
    }

    // ---------------- ekran görüntüsü kipi (CI) ----------------

    /// <summary>Pencereyi (ve istenirse bir iletişim kutusunu) PNG olarak kaydeder. Program --screenshot ile çağırır.</summary>
    public async Task CaptureAsync(string file, string? dialog)
    {
        ApplyFull();
        RefreshInfo();
        await Task.Delay(1200);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        using (var bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
        }
        // Ayarlar kartının aşağısı (Görünüm, Arşiv, Kullanım Limitleri, hata raporu, Hakkında, iletişim)
        var card = settingsFlow.Parent!;
        int view = settingsFlow.ClientSize.Height, total = settingsFlow.DisplayRectangle.Height;
        for (int i = 1, y = (int)(view * 0.85); y < total && i <= 4; i++, y += (int)(view * 0.85))
        {
            settingsFlow.AutoScrollPosition = new Point(0, y);
            settingsFlow.PerformLayout();
            await Task.Delay(300);
            using var cb = new Bitmap(card.Width, card.Height);
            card.DrawToBitmap(cb, new Rectangle(0, 0, card.Width, card.Height));
            cb.Save(Path.ChangeExtension(file, null) + "-ayarlar" + i + ".png", System.Drawing.Imaging.ImageFormat.Png);
        }
        settingsFlow.AutoScrollPosition = Point.Empty;
        if (dialog == null) return;
        Form f = dialog switch
        {
            "onboarding" => new OnboardingForm((_, _, _, _) => { }),
            "whatsnew" => new WhatsNewForm(Updater.CurrentText, WhatsNewForm.NotesFor(Updater.CurrentText)),
            "contact" => new ContactForm(),
            "archive" => new ArchiveForm(store),
            _ => new ContactForm(),
        };
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(40, 40);
        f.Show(this);
        await Task.Delay(800);
        if (f is OnboardingForm ob) { ob.Next(); await Task.Delay(500); }
        using (var bmp = new Bitmap(f.Width, f.Height))
        {
            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
            bmp.Save(Path.ChangeExtension(file, null) + "-" + dialog + ".png", System.Drawing.Imaging.ImageFormat.Png);
        }
        f.Close();
    }
}
