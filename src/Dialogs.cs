using System.Diagnostics;

namespace DDS;

/// <summary>One-click update window: shows percent, MB done / total, MB remaining and download speed.</summary>
public sealed class UpdateForm : Form
{
    readonly Label lTitle, lInfo;
    readonly ProgressBar bar;
    readonly Button bCancel;
    readonly CancellationTokenSource cts = new();
    UpdateInfo? info;
    bool downloading;

    public bool Installed { get; private set; }

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public UpdateForm(UpdateInfo? known)
    {
        info = known;
        Text = Lang.T("upd_title");
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(D(600), D(190));
        RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = Lang.IsRtl;

        lTitle = new Label { Left = D(24), Top = D(20), Width = D(552), Height = D(28), Font = new Font("Segoe UI Semibold", 12f) };
        bar = new ProgressBar { Left = D(24), Top = D(60), Width = D(552), Height = D(22), Minimum = 0, Maximum = 1000 };
        lInfo = new Label { Left = D(24), Top = D(92), Width = D(552), Height = D(26), ForeColor = Theme.Sub };
        bCancel = Theme.ActionButton(Lang.T("cancel"), Theme.Card2);
        bCancel.AutoSize = false;
        bCancel.SetBounds(D(456), D(136), D(120), D(36));
        bCancel.Click += (_, _) => { if (downloading) cts.Cancel(); else Close(); };
        Controls.AddRange(new Control[] { lTitle, bar, lInfo, bCancel });

        Shown += async (_, _) => await RunAsync();
    }

    async Task RunAsync()
    {
        try
        {
            if (info == null)
            {
                lTitle.Text = Lang.T("upd_checking");
                bar.Style = ProgressBarStyle.Marquee;
                info = await Updater.CheckAsync();
                bar.Style = ProgressBarStyle.Continuous;
                if (info == null)
                {
                    lTitle.Text = Lang.F("upd_latest", Updater.CurrentText);
                    bar.Value = bar.Maximum;
                    bCancel.Text = Lang.T("ok");
                    return;
                }
            }

            lTitle.Text = Lang.F("upd_downloading", info.Version.ToString(3));
            downloading = true;
            Progress(0, info.Size, 0);
            var file = await Updater.DownloadAsync(info, (d, t, s) =>
            {
                if (InvokeRequired) BeginInvoke(() => Progress(d, t, s)); else Progress(d, t, s);
            }, cts.Token);
            downloading = false;

            lTitle.Text = Lang.T("upd_installing");
            bCancel.Enabled = false;
            Updater.ApplyAndRestart(file);
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
            lTitle.Text = Lang.F("upd_fail", ex.Message);
            bCancel.Text = Lang.T("ok");
        }
    }

    void Progress(long done, long total, double speed)
    {
        const double MB = 1024.0 * 1024.0;
        int pct = total > 0 ? (int)Math.Min(100, done * 100 / total) : 0;
        bar.Value = total > 0 ? (int)Math.Min(bar.Maximum, done * bar.Maximum / total) : 0;
        lInfo.Text = Lang.F("upd_progress", pct, done / MB, total / MB, Math.Max(0, total - done) / MB, speed / MB);
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
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(D(580), D(390));
        RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = Lang.IsRtl;

        var msg = new Label { Text = Lang.T("sync_msg"), Left = D(24), Top = D(18), Width = D(532), Height = D(205) };
        var create = Theme.ActionButton(Lang.T("sync_create"), Theme.Accent);
        create.Location = new Point(D(24), D(228));
        create.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(GitHubSync.TokenUrl) { UseShellExecute = true }); } catch { }
        };
        box = new TextBox
        {
            Left = D(24), Top = D(284), Width = D(532), BackColor = Theme.Card2, ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "ghp_...",
        };
        var ok = Theme.ActionButton(Lang.T("ok"), Theme.Green);
        ok.AutoSize = false;
        ok.SetBounds(D(306), D(334), D(120), D(36));
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.ActionButton(Lang.T("cancel"), Theme.Card2);
        cancel.AutoSize = false;
        cancel.SetBounds(D(436), D(334), D(120), D(36));
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { msg, create, box, ok, cancel });
    }
}
