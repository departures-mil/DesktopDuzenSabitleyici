using System.Text;
using System.Text.Json;

namespace DDS;

/// <summary>"Admin ile iletişime geç": öneri / hata bildirimi doğrudan yöneticinin e-postasına gider (FormSubmit).</summary>
public sealed class ContactForm : Form
{
    readonly TextBox tName, tMail, tMsg;
    readonly Label lStatus;
    readonly Button bSend;

    int D(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    public ContactForm()
    {
        Text = Lang.T("contact_title");
        Icon = Theme.AppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(D(560), D(470));
        RightToLeft = Lang.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = Lang.IsRtl;

        var lDesc = new Label { Left = D(20), Top = D(16), Width = D(520), Height = D(48), ForeColor = Theme.Sub, Text = Lang.T("contact_desc") };
        var lName = new Label { Left = D(20), Top = D(70), Width = D(520), Height = D(22), Text = Lang.T("contact_name") };
        tName = Box(D(94), false); tName.MaxLength = 80;
        var lMail = new Label { Left = D(20), Top = D(130), Width = D(520), Height = D(22), Text = Lang.T("contact_mail") };
        tMail = Box(D(154), false); tMail.MaxLength = 120; tMail.RightToLeft = RightToLeft.No;
        var lMsg = new Label { Left = D(20), Top = D(190), Width = D(520), Height = D(22), Text = Lang.T("contact_msg") };
        tMsg = Box(D(214), true); tMsg.MaxLength = 5000; tMsg.Height = D(160);
        lStatus = new Label { Left = D(20), Top = D(384), Width = D(520), Height = D(26), ForeColor = Theme.Sub };
        bSend = Theme.ActionButton("✉ " + Lang.T("contact_send"), Theme.Accent);
        bSend.AutoSize = false;
        bSend.SetBounds(D(20), D(418), D(160), D(38));
        var bClose = Theme.ActionButton(Lang.T("cancel"), Theme.Card2);
        bClose.AutoSize = false;
        bClose.SetBounds(D(420), D(418), D(120), D(38));
        bClose.Click += (_, _) => Close();
        bSend.Click += async (_, _) => await SendAsync();
        Controls.AddRange(new Control[] { lDesc, lName, tName, lMail, tMail, lMsg, tMsg, lStatus, bSend, bClose });
    }

    TextBox Box(int top, bool multi) => new()
    {
        Left = D(20), Top = top, Width = D(520), Multiline = multi, ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None,
        BackColor = Theme.Card2, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
    };

    async Task SendAsync()
    {
        var msg = tMsg.Text.Trim();
        var mail = tMail.Text.Trim();
        if (msg.Length == 0) { lStatus.Text = Lang.T("contact_empty"); return; }
        if (mail.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(mail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) { lStatus.Text = Lang.T("contact_bad"); return; }
        bSend.Enabled = false;
        lStatus.Text = Lang.T("contact_sending");
        try
        {
            var to = new string(Encoding.UTF8.GetString(Convert.FromBase64String("bW9jLmxpYW1ub3RvcnBANDMyMWxkaW50bWxkYmE=")).Reverse().ToArray());
            var body = new Dictionary<string, string>
            {
                ["_subject"] = "Desktop Düzen Sabitleyici: yeni mesaj", ["_template"] = "table", ["_captcha"] = "false",
                ["Ad"] = tName.Text.Trim().Length > 0 ? tName.Text.Trim() : "-", ["Mesaj"] = msg,
                ["Surum"] = Updater.CurrentText, ["Cihaz"] = "Windows " + Environment.OSVersion.Version, ["Dil"] = Lang.Current,
            };
            if (mail.Length > 0) { body["email"] = mail; body["_replyto"] = mail; }
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://formsubmit.co/ajax/" + to)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            req.Headers.Accept.ParseAdd("application/json");
            req.Headers.Referrer = new Uri("https://departures-mil.github.io/DesktopDuzenSabitleyici/");
            req.Headers.TryAddWithoutValidation("Origin", "https://departures-mil.github.io");
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DesktopDuzenSabitleyici/" + Updater.CurrentText);
            using var res = await http.SendAsync(req);
            var txt = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode && !txt.Contains("activat", StringComparison.OrdinalIgnoreCase)) throw new Exception(((int)res.StatusCode).ToString());
            tMsg.Text = "";
            lStatus.Text = Lang.T("contact_ok");
        }
        catch
        {
            lStatus.Text = Lang.T("contact_fail");
        }
        finally { bSend.Enabled = true; }
    }
}
