using System.Diagnostics;
using DDS.Native;
using WinTimer = System.Windows.Forms.Timer;

namespace DDS;

public sealed class MainForm : Form
{
    readonly Settings settings = Storage.LoadSettings();
    readonly StoreData store = Storage.LoadStore();
    readonly Guardian guard;
    readonly NotifyIcon tray;
    readonly ToolTip tips = new() { InitialDelay = 300 };
    readonly EventWaitHandle showEvent;
    readonly List<(DateTime t, string key, object[] args)> logs = new();
    readonly WinTimer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    readonly WinTimer autoSyncTimer = new() { Interval = 5 * 60 * 1000 };

    bool startHidden, reallyExit, half, loading = true, started, syncing, shutdownSaved;
    DateTime? lastSyncedSave;
    UpdateInfo? update;

    // UI
    Panel titleBar = null!; Label titleLbl = null!; PictureBox logo = null!;
    Button bUpdate = null!, bSync = null!, bMin = null!, bHalf = null!, bClose = null!;
    TableLayoutPanel content = null!;
    Label hStatus = null!, lStatus = null!, lHow = null!, lScreensH = null!, lScreens = null!, lIcons = null!, lSave = null!, lRestore = null!;
    Button bProtect = null!, bSaveNow = null!, bRestore = null!;
    Label hSettings = null!, lMode = null!, lLang = null!;
    Button bContact = null!;
    CheckBox cAuto = null!, cStartup = null!, cAutoSave = null!;
    Label lSaveInfo = null!, lModeInfo = null!;
    ComboBox cLang = null!;
    Label hProfiles = null!, hLog = null!;
    ListView lv = null!;
    Button bDel = null!, bRestoreSel = null!;
    ListBox lb = null!;
    ContextMenuStrip trayMenu = null!;
    ToolStripMenuItem miOpen = null!, miSave = null!, miRestore = null!, miExit = null!;

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public MainForm(bool startHidden)
    {
        this.startHidden = startHidden;
        Lang.Current = settings.Lang;

        Text = Lang.T("app_title");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10f);
        Icon = Theme.AppIcon();
        DoubleBuffered = true;
        MinimumSize = new Size(D(780), D(540));

        BuildUI();

        tray = new NotifyIcon { Icon = Icon, Visible = true, Text = "Desktop Düzen Sabitleyici", ContextMenuStrip = trayMenu };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };

        guard = new Guardian(store, settings);
        guard.Log += OnLog;
        guard.Changed += RefreshInfo;

        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) =>
        {
            try { if (IsHandleCreated) BeginInvoke(ShowMain); } catch { }
        }, null, -1, false);

        Installer.SetStartup(settings.StartWithWindows);
        updateTimer.Tick += async (_, _) => await CheckUpdateSilently();
        autoSyncTimer.Tick += async (_, _) => await AutoSync();

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
        Win32.RegisterHotKey(Handle, 1, 0x0001 | 0x0002 | 0x4000, (uint)Keys.S); // Ctrl+Alt+S (no repeat)
        if (started) return;
        started = true;
        BeginInvoke(async () =>
        {
            guard.Start();
            RefreshInfo();
            updateTimer.Start();
            autoSyncTimer.Start();
            await Task.Delay(4000);
            await CheckUpdateSilently();
        });
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyFull();
    }

    void ApplyFull()
    {
        half = false;
        WindowState = FormWindowState.Normal;
        Padding = Padding.Empty;
        Bounds = Screen.FromPoint(Cursor.Position).WorkingArea;
        UpdateHalfGlyph();
    }

    void ApplyHalf()
    {
        half = true;
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
        tips.SetToolTip(bHalf, Lang.T(half ? "tt_full" : "tt_half"));
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84, WM_QUERYENDSESSION = 0x11, WM_HOTKEY = 0x312;
        if (m.Msg == WM_QUERYENDSESSION && !shutdownSaved)
        {
            shutdownSaved = true;
            guard.SaveBeforeShutdown("shutdown", "log_shutdown");
        }
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
    }

    void HideToTray()
    {
        Hide();
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
        tray.Visible = false;
        guard.Dispose();
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
            Text = glyph, Font = new Font(Theme.IconFont, 10f), Width = D(54), Height = D(44),
            FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text, BackColor = Theme.TitleBg,
            Margin = Padding.Empty, TabStop = false, Cursor = Cursors.Hand, UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = danger ? Color.FromArgb(232, 17, 35) : Theme.Hover;
        b.Click += (_, _) => click();
        return b;
    }

    static Label L(Font? f = null, Color? c = null) => new()
    {
        AutoSize = true, Font = f, ForeColor = c ?? Theme.Text, Margin = new Padding(0, 4, 0, 4), BackColor = Color.Transparent,
    };

    Panel Card(Control inner)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(D(20)), Margin = new Padding(D(8)) };
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
        f.SizeChanged += (_, _) =>
        {
            foreach (Control c in f.Controls)
                if (c is Label or RadioButton or CheckBox)
                    c.MaximumSize = new Size(Math.Max(100, f.ClientSize.Width - D(12)), 0);
        };
        return f;
    }

    void BuildUI()
    {
        var hFont = new Font("Segoe UI Semibold", 13f);

        // ---- title bar with the window buttons in the top-right corner ----
        titleBar = new Panel { Dock = DockStyle.Top, Height = D(44), BackColor = Theme.TitleBg };
        var btns = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.TitleBg,
            Margin = Padding.Empty, Padding = Padding.Empty,
        };
        bClose = WinButton(Theme.GClose, () => Close(), danger: true);
        bHalf = WinButton(Theme.GRestore, ToggleHalf);
        bMin = WinButton(Theme.GMin, () => WindowState = FormWindowState.Minimized);
        bSync = WinButton(Theme.GSync, async () => await SyncClicked());
        bUpdate = WinButton(Theme.GDownload, UpdateClicked);
        btns.Controls.AddRange(new Control[] { bClose, bHalf, bMin, bSync, bUpdate });

        logo = new PictureBox
        {
            Image = Icon!.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Width = D(26), Height = D(26),
            Left = D(12), Top = D(9), BackColor = Theme.TitleBg,
        };
        titleLbl = new Label
        {
            AutoSize = true, Left = D(46), Top = D(11), Font = new Font("Segoe UI Semibold", 11f),
            ForeColor = Theme.Text, BackColor = Theme.TitleBg,
        };
        titleBar.Controls.Add(btns);
        titleBar.Controls.Add(logo);
        titleBar.Controls.Add(titleLbl);
        titleBar.MouseDown += DragWindow;
        titleLbl.MouseDown += DragWindow;
        logo.MouseDown += DragWindow;

        // ---- content grid ----
        content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(D(14)), BackColor = Theme.Bg,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        // status card
        var fs = Flow();
        hStatus = L(hFont);
        lStatus = L(new Font("Segoe UI Semibold", 12f));
        lHow = L(null, Theme.Sub);
        lScreensH = L(new Font("Segoe UI Semibold", 10f), Theme.Sub);
        lScreens = L();
        lIcons = L();
        lSave = L();
        lRestore = L();
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = new Padding(0, D(8), 0, 0) };
        bProtect = Theme.ActionButton("", Theme.Card2);
        bSaveNow = Theme.ActionButton("", Theme.Accent);
        bRestore = Theme.ActionButton("", Theme.Green);
        bProtect.Click += (_, _) => ToggleProtect();
        bSaveNow.Click += (_, _) => guard.SaveNow("manual");
        bRestore.Click += (_, _) => guard.RestoreNow();
        actions.Controls.AddRange(new Control[] { bSaveNow, bRestore, bProtect });
        fs.Controls.AddRange(new Control[] { hStatus, lStatus, lHow, lScreensH, lScreens, lIcons, lSave, lRestore, actions });
        fs.SizeChanged += (_, _) => actions.MaximumSize = new Size(Math.Max(100, fs.ClientSize.Width - D(12)), 0);

        // settings card
        var fset = Flow();
        hSettings = L(hFont);
        lMode = L(new Font("Segoe UI Semibold", 10f), Theme.Sub);
        lModeInfo = L(new Font("Segoe UI", 9.5f), Theme.Text);
        lModeInfo.Margin = new Padding(0, 4, 0, 10);
        cAuto = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4) };
        cStartup = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4) };
        cAutoSave = new CheckBox { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4) };
        lSaveInfo = L(new Font("Segoe UI", 9f), Theme.Sub);
        lSaveInfo.Margin = new Padding(0, 2, 0, 10);
        lLang = L(new Font("Segoe UI Semibold", 10f), Theme.Sub);
        cLang = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = D(220), FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Card2, ForeColor = Theme.Text,
        };
        foreach (var (_, name) in Lang.Languages) cLang.Items.Add(name);
        cAuto.Checked = settings.DisableAutoArrange;
        cStartup.Checked = settings.StartWithWindows;
        cAutoSave.Checked = settings.AutoSave;
        cAutoSave.CheckedChanged += (_, _) => { if (!loading) { settings.AutoSave = cAutoSave.Checked; Storage.SaveSettings(settings); } };
        cLang.SelectedIndex = Math.Max(0, Array.FindIndex(Lang.Languages, x => x.code == settings.Lang));
        cAuto.CheckedChanged += (_, _) => { if (!loading) { settings.DisableAutoArrange = cAuto.Checked; Storage.SaveSettings(settings); } };
        cStartup.CheckedChanged += (_, _) =>
        {
            if (loading) return;
            settings.StartWithWindows = cStartup.Checked;
            Storage.SaveSettings(settings);
            Installer.SetStartup(settings.StartWithWindows);
        };
        cLang.SelectedIndexChanged += (_, _) =>
        {
            if (loading || cLang.SelectedIndex < 0) return;
            settings.Lang = Lang.Languages[cLang.SelectedIndex].code;
            Storage.SaveSettings(settings);
            ApplyLanguage();
        };
        bContact = Theme.ActionButton("", Theme.Card2);
        bContact.Margin = new Padding(0, D(14), 0, 0);
        bContact.Click += (_, _) => { using var f = new ContactForm(); f.ShowDialog(this); };
        fset.Controls.AddRange(new Control[] { hSettings, lMode, lModeInfo, cAuto, cAutoSave, cStartup, lSaveInfo, lLang, cLang, bContact });

        // profiles card
        var tp = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, BackColor = Theme.Card };
        tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        hProfiles = L(hFont);
        lv = new ListView
        {
            View = View.Details, FullRowSelect = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BackColor = Theme.Card2, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
            OwnerDraw = true, Margin = new Padding(0, D(6), 0, D(6)),
        };
        lv.Columns.Add("", 300);
        lv.Columns.Add("", 60);
        lv.Columns.Add("", 140);
        lv.Columns.Add("", 120);
        lv.MultiSelect = false;
        lv.DoubleClick += (_, _) => RestoreSelected();
        lv.DrawColumnHeader += (_, e) =>
        {
            using var bg = new SolidBrush(Theme.Card);
            e.Graphics.FillRectangle(bg, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, new Font("Segoe UI Semibold", 9f), e.Bounds, Theme.Sub,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                (Lang.IsRtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0));
        };
        lv.DrawItem += (_, e) => e.DrawDefault = true;
        lv.DrawSubItem += (_, e) => e.DrawDefault = true;
        lv.Resize += (_, _) => FitColumns();
        bDel = Theme.ActionButton("", Theme.Red);
        bDel.Click += (_, _) => DeleteSelected();
        bRestoreSel = Theme.ActionButton("", Theme.Green);
        bRestoreSel.Click += (_, _) => RestoreSelected();
        var hb = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = Padding.Empty };
        hb.Controls.AddRange(new Control[] { bRestoreSel, bDel });
        tp.Controls.Add(hProfiles, 0, 0);
        tp.Controls.Add(lv, 0, 1);
        tp.Controls.Add(hb, 0, 2);

        // log card
        var tl = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Theme.Card };
        tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hLog = L(hFont);
        lb = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.Card2, ForeColor = Theme.Sub, BorderStyle = BorderStyle.None,
            IntegralHeight = false, Margin = new Padding(0, D(6), 0, 0), Font = new Font("Segoe UI", 9.5f),
        };
        tl.Controls.Add(hLog, 0, 0);
        tl.Controls.Add(lb, 0, 1);

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
        lv.Columns[3].Width = D(120);
        lv.Columns[0].Width = Math.Max(D(120), w - D(60) - D(130) - D(120) - 4);
    }

    // ---------------- texts ----------------

    void ApplyLanguage()
    {
        Lang.Current = settings.Lang;
        bool rtl = Lang.IsRtl;
        content.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        trayMenu.RightToLeft = content.RightToLeft;

        Text = $"{Lang.T("app_title")} v{Updater.CurrentText}";
        titleLbl.Text = Text;
        hStatus.Text = Lang.T("status_hdr");
        lHow.Text = Lang.T("how_it_works");
        lScreensH.Text = Lang.T("screens");
        bSaveNow.Text = Lang.T("save_now");
        bRestore.Text = Lang.T("restore_now");
        hSettings.Text = Lang.T("settings_hdr");
        lMode.Text = Lang.T("mode");
        lModeInfo.Text = Lang.T("mode_info");
        cAuto.Text = Lang.T("autoarr");
        cStartup.Text = Lang.T("startup");
        cAutoSave.Text = Lang.T("autosave");
        lSaveInfo.Text = Lang.T("save_info");
        lLang.Text = Lang.T("language");
        bContact.Text = "✉ " + Lang.T("contact_title");
        hProfiles.Text = Lang.T("profiles_hdr");
        lv.Columns[0].Text = Lang.T("col_display");
        lv.Columns[1].Text = Lang.T("col_icons");
        lv.Columns[2].Text = Lang.T("col_date");
        lv.Columns[3].Text = Lang.T("col_src");
        bRestoreSel.Text = Lang.T("restore_sel");
        lv.RightToLeftLayout = rtl;
        bDel.Text = Lang.T("delete");
        hLog.Text = Lang.T("log_hdr");
        miOpen.Text = Lang.T("tray_open");
        miSave.Text = Lang.T("save_now");
        miRestore.Text = Lang.T("restore_now");
        miExit.Text = Lang.T("tray_exit");

        tips.SetToolTip(bSync, Lang.T("tt_sync"));
        tips.SetToolTip(bMin, Lang.T("tt_min"));
        tips.SetToolTip(bClose, Lang.T("tt_close"));
        UpdateHalfGlyph();
        UpdateUpdateButton();

        lb.Items.Clear();
        foreach (var e in logs) lb.Items.Add(FormatLog(e));
        RefreshInfo();
        // re-run wrapping with the new texts
        foreach (Control card in content.Controls)
            foreach (Control inner in card.Controls)
                if (inner is FlowLayoutPanel f) f.PerformLayout();
    }

    static string When(DateTime? t) =>
        t == null ? Lang.T("never") : t.Value.Date == DateTime.Today ? t.Value.ToString("HH:mm:ss") : t.Value.ToString("dd.MM.yyyy HH:mm");

    void RefreshInfo()
    {
        if (guard == null) return;
        bool on = settings.Protect;
        lStatus.Text = Lang.T(on ? "prot_on" : "prot_off");
        lStatus.ForeColor = on ? Theme.Green : Theme.Red;
        bProtect.Text = Lang.T(on ? "btn_prot_off" : "btn_prot_on");

        var mons = guard.Monitors.Count > 0 ? guard.Monitors : DisplayInfo.GetMonitors();
        lScreens.Text = string.Join("\n", mons.Select((m, i) =>
            $"{i + 1})  {m.Bounds.Width}×{m.Bounds.Height}   %{m.Dpi * 100 / 96}   {m.Model}{(m.Primary ? "   — " + Lang.T("primary") : "")}"));
        lIcons.Text = Lang.F("icons", guard.IconCount);
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
            string name = p.Display.Length > 0 ? p.Display : p.Key;
            for (int i = 0; i < p.History.Count; i++)
            {
                var h = p.History[i];
                bool top = i == 0;
                var it = new ListViewItem(name + (cur && top ? $"   ◀ {Lang.T("current")}" : ""))
                {
                    Tag = h.Id,
                    ForeColor = cur && top ? Theme.Green : top ? Theme.Text : Theme.Sub,
                };
                it.SubItems.Add(h.Icons.Count.ToString());
                it.SubItems.Add(h.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
                it.SubItems.Add(Lang.T("src_" + h.Source));
                if (h.Id == sel) it.Selected = true;
                lv.Items.Add(it);
            }
        }
        lv.EndUpdate();
        FitColumns();
    }

    string? SelectedSnap() => lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as string : null;

    void RestoreSelected()
    {
        if (SelectedSnap() is string id) guard.RestoreSnapshot(id);
    }

    void DeleteSelected()
    {
        if (SelectedSnap() is not string id || store.Find(id) is not { } f) return;
        f.p.History.Remove(f.s);
        if (f.p.History.Count == 0) store.Displays.Remove(f.p.Key);
        Storage.SaveStore(store);
        RefreshProfiles();
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

    void UpdateUpdateButton()
    {
        if (update != null)
        {
            bUpdate.ForeColor = Theme.Green;
            bUpdate.BackColor = Color.FromArgb(20, 60, 40);
            tips.SetToolTip(bUpdate, Lang.F("tt_update_new", update.Version.ToString(3)));
        }
        else
        {
            bUpdate.ForeColor = Theme.Text;
            bUpdate.BackColor = Theme.TitleBg;
            tips.SetToolTip(bUpdate, Lang.T("tt_update"));
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
                tray.ShowBalloonTip(4000, Lang.T("app_title"), Lang.F("tt_update_new", u.Version.ToString(3)), ToolTipIcon.Info);
            }
        }
        catch { }
    }

    void UpdateClicked()
    {
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
        }
        base.Dispose(disposing);
    }
}
