using System.Diagnostics;
using DDS.Core;

namespace DDS;

/// <summary>Tek tıkla güncelleme penceresi: yüzde · MB inen / toplam · kalan · hız; yama kipinde "yalnızca değişen kısımlar".</summary>
public sealed class UpdateForm : Form
{
    readonly Label lTitle, lInfo, lMode;
    readonly ProgressBar bar;
    readonly Button bCancel;
    readonly CancellationTokenSource cts = new();
    UpdateInfo? info;
    bool downloading, patch;
    ProgressTracker? tracker;

    public bool Installed { get; private set; }

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public UpdateForm(UpdateInfo? known)
    {
        info = known;
        Text = CoreStrings.T("fc.update");
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.Dialog(this);
        ClientSize = new Size(D(620), D(220));

        lTitle = new Label { Left = D(24), Top = D(20), Width = D(572), Height = D(30), Font = Theme.F(12.5f, true), ForeColor = Theme.Text };
        lMode = new Label { Left = D(24), Top = D(52), Width = D(572), Height = D(22), Font = Theme.F(9.5f, true), ForeColor = Theme.Accent };
        bar = new ProgressBar { Left = D(24), Top = D(80), Width = D(572), Height = D(10), Minimum = 0, Maximum = 1000 };
        lInfo = new Label { Left = D(24), Top = D(100), Width = D(572), Height = D(46), ForeColor = Theme.Sub, Font = Theme.F(10f) };
        bCancel = Theme.ActionButton(CoreStrings.T("fc.cancel"), Btn.Secondary);
        bCancel.AutoSize = false;
        bCancel.SetBounds(Lang.IsRtl ? D(24) : D(456), D(160), D(140), D(40));
        bCancel.Click += (_, _) => { if (downloading) cts.Cancel(); else Close(); };
        Controls.AddRange(new Control[] { lTitle, lMode, bar, lInfo, bCancel });

        Shown += async (_, _) => await RunAsync();
    }

    async Task RunAsync()
    {
        try
        {
            if (info == null)
            {
                lTitle.Text = CoreStrings.T("fc.update.checking");
                bar.Style = ProgressBarStyle.Marquee;
                info = await Updater.CheckAsync();
                bar.Style = ProgressBarStyle.Continuous;
                if (info == null)
                {
                    lTitle.Text = CoreStrings.T1("fc.update.none", "v", Updater.CurrentText);
                    bar.Value = bar.Maximum;
                    bCancel.Text = CoreStrings.T("fc.ok");
                    return;
                }
            }

            var ver = info.Version.ToString(3);
            lTitle.Text = CoreStrings.T1("fc.update.avail", "v", ver) + " — " + CoreStrings.T("fc.update.downloading");
            if (info.NeedsSetup) lInfo.Text = Lang.T("setup_needed");
            downloading = true;
            var file = await Updater.DownloadAsync(info, (d, t) =>
            {
                if (InvokeRequired) BeginInvoke(() => Progress(d, t)); else Progress(d, t);
            }, cts.Token, p =>
            {
                void Set()
                {
                    if (patch && !p) lMode.Text = CoreStrings.T("fc.update.fallback");
                    else lMode.Text = p ? "⚡ " + CoreStrings.T("fc.update.patchMode") : CoreStrings.T("fc.update.fullMode");
                    patch = p;
                    tracker = null;
                }
                if (InvokeRequired) BeginInvoke(Set); else Set();
            });
            downloading = false;

            lTitle.Text = CoreStrings.T(info.NeedsSetup ? "fc.update.installing" : "fc.update.restarting");
            bCancel.Enabled = false;
            Updater.ApplyAndRestart(file, info);
            Installed = true;
            await Task.Delay(700);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception ex)
        {
            downloading = false;
            bar.Style = ProgressBarStyle.Continuous;
            lTitle.Text = CoreStrings.T("fc.error");
            lInfo.Text = CoreStrings.T1("fc.update.failed", "e", ex is HttpRequestException ? CoreStrings.T("fc.update.offline") : ex.Message);
            bCancel.Text = CoreStrings.T("fc.ok");
        }
    }

    void Progress(long done, long total)
    {
        tracker ??= new ProgressTracker(total);
        tracker.SetTotal(total);
        var p = tracker.Update(done, total > 0 && done >= total);
        bar.Value = total > 0 ? (int)Math.Min(bar.Maximum, done * bar.Maximum / total) : 0;
        lInfo.Text = p.Format(patch);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) cts.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Asks once for a GitHub key (gist permission) used to back up and sync layouts.</summary>
public sealed class TokenForm : Form
{
    readonly TextBox box;
    public string Token => box.Text.Trim();

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public TokenForm()
    {
        Text = Lang.T("sync_title");
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.Dialog(this);
        ClientSize = new Size(D(580), D(400));

        var msg = new Label { Text = Lang.T("sync_msg"), Left = D(24), Top = D(18), Width = D(532), Height = D(205) };
        var create = Theme.ActionButton(Lang.T("sync_create"), Btn.Primary);
        create.Location = new Point(D(24), D(228));
        create.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(GitHubSync.TokenUrl) { UseShellExecute = true }); } catch { }
        };
        box = new TextBox
        {
            Left = D(24), Top = D(284), Width = D(532), BackColor = Theme.Input, ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "ghp_...",
        };
        var ok = Theme.ActionButton(Lang.T("ok"), Btn.Primary);
        ok.AutoSize = false;
        ok.SetBounds(D(296), D(340), D(125), D(40));
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.ActionButton(Lang.T("cancel"), Btn.Secondary);
        cancel.AutoSize = false;
        cancel.SetBounds(D(431), D(340), D(125), D(40));
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { msg, create, box, ok, cancel });
    }
}
