using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>What the user chose in the offer of a new version.</summary>
public enum UpdateChoice { Install, Later, Skip }

/// <summary>
/// The windows of the update of the stand-alone app: the offer (Install / Later / Skip this version) and the download with its progress. Never part of the Opsis plugin:
/// inside Opsis the plugin is updated by the update button of Opsis.
/// </summary>
public static class UpdateUi
{
    public static UpdateChoice Offer(IWin32Window? owner, UpdateInfo info)
    {
        var p = ChronikologioTheme.Palette;
        var choice = UpdateChoice.Later;
        using var form = new Form
        {
            Text = Loc.T("upd.title"), Size = new Size(620, 520), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog, BackColor = p.Background, ForeColor = p.Text, Font = new Font("Segoe UI", 10f), ShowInTaskbar = false
        };
        var head = new Label { Dock = DockStyle.Top, Height = 64, Text = string.Format(Loc.T("upd.heading"), info.Version.ToString(3)) + "\n" + string.Format(Loc.T("upd.current"), UpdateChecker.Norm(UpdateChecker.Current)), Font = new Font("Segoe UI", 13f, FontStyle.Bold), Padding = new Padding(10, 10, 0, 0) };
        var notes = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = p.Background, ForeColor = p.Text, DetectUrls = false, Text = info.Notes };
        ThemeNative.StyleScrollbars(notes);
        var warn = new Label { Dock = DockStyle.Bottom, Height = 60, Text = Loc.T("upd.opsis"), ForeColor = p.TextMuted, Padding = new Padding(10, 4, 10, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 8, 8, 0) };
        Opsis.UI.FlatButton Button(string key, UpdateChoice result, int width)
        {
            var b = new Opsis.UI.FlatButton { Text = Loc.T(key), Width = width, Height = 34 };
            b.Click += (_, _) => { choice = result; form.Close(); };
            buttons.Controls.Add(b);
            return b;
        }
        Button("upd.install", UpdateChoice.Install, 130);
        Button("upd.later", UpdateChoice.Later, 110);
        Button("upd.skip", UpdateChoice.Skip, 230);
        form.Controls.Add(notes);
        form.Controls.Add(warn);
        form.Controls.Add(buttons);
        form.Controls.Add(head);
        TitleBar.Apply(form.Handle, p.IsDark);
        form.ShowDialog(owner);
        return choice;
    }

    /// <summary>Downloads the installer with a progress window, checks its SHA-256 and starts it. Returns true when the installer was started (the app should close).</summary>
    public static async Task<bool> DownloadAndStartAsync(IWin32Window? owner, UpdateInfo info)
    {
        var p = ChronikologioTheme.Palette;
        using var form = new Form
        {
            Text = Loc.T("upd.title"), Size = new Size(460, 160), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ControlBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog, BackColor = p.Background, ForeColor = p.Text, Font = new Font("Segoe UI", 10f), ShowInTaskbar = false
        };
        var label = new Label { Left = 16, Top = 18, Width = 410, Height = 24, Text = string.Format(Loc.T("upd.downloading"), 0) };
        var bar = new ProgressBar { Left = 16, Top = 52, Width = 410, Height = 18, Maximum = 100 };
        form.Controls.Add(label);
        form.Controls.Add(bar);
        TitleBar.Apply(form.Handle, p.IsDark);
        form.Show(owner);
        try
        {
            var progress = new Progress<int>(v => { bar.Value = Math.Clamp(v, 0, 100); label.Text = string.Format(Loc.T("upd.downloading"), v); });
            var path = await new UpdateInstaller().DownloadAsync(info, UpdateInstaller.UpdateFolder, progress);
            label.Text = Loc.T("upd.verified");
            if (!UpdateInstaller.Launch(path)) { Failed(form, new UpdateException(UpdateFailure.Untrusted, "signature")); return false; }
            return true;
        }
        catch (UpdateException ex)
        {
            UpdateInstaller.Log($"failed: {ex.Failure}: {ex.Message}");
            form.Close();
            Failed(owner, ex);
            return false;
        }
    }

    private static void Failed(IWin32Window? owner, UpdateException ex)
    {
        var text = ex.Failure switch
        {
            UpdateFailure.HashMismatch => Loc.T("upd.hashfail"),
            UpdateFailure.NoHash or UpdateFailure.Untrusted => Loc.T("upd.noinstaller"),
            UpdateFailure.Network => Loc.T("upd.nonet"),
            _ => string.Format(Loc.T("upd.failed"), ex.Message)
        };
        MessageBox.Show(owner, text, Loc.T("upd.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Message for a result of a check that the user asked for by hand.</summary>
    public static void ShowManualResult(IWin32Window? owner, UpdateCheckResult r)
    {
        var text = r.Outcome switch
        {
            UpdateOutcome.UpToDate => string.Format(Loc.T("upd.uptodate"), UpdateChecker.Norm(UpdateChecker.Current)),
            UpdateOutcome.NoNetwork => Loc.T("upd.nonet"),
            UpdateOutcome.BadAnswer => Loc.T("upd.bad"),
            UpdateOutcome.NoInstaller => Loc.T("upd.noinstaller"),
            _ => ""
        };
        if (text.Length > 0) MessageBox.Show(owner, text, Loc.T("upd.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
