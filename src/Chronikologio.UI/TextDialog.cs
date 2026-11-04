using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>A read-only window with a title, a body text and a close button, in the colours of the current theme (used for the guide and the version history).</summary>
public static class TextDialog
{
    public static void Show(IWin32Window? owner, string title, string heading, string body)
    {
        var p = ChronikologioTheme.Palette;
        using var form = new Form
        {
            Text = title,
            Size = new Size(640, 560),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            BackColor = p.Background,
            ForeColor = p.Text,
            Font = new Font("Segoe UI", 10f)
        };
        var box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = p.Background,
            ForeColor = p.Text,
            DetectUrls = false,
            Text = body
        };
        ThemeNative.StyleScrollbars(box);
        var head = new Label { Dock = DockStyle.Top, Height = 40, Text = heading, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = p.Text, BackColor = p.Background, Padding = new Padding(8, 8, 0, 0) };
        var close = new Opsis.UI.FlatButton { Text = "OK", Dock = DockStyle.Bottom, Height = 36 };
        close.Click += (_, _) => form.Close();
        form.Controls.Add(box);
        form.Controls.Add(close);
        form.Controls.Add(head);
        form.AcceptButton = null;
        form.CancelButton = null;
        form.KeyPreview = true;
        form.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) form.Close(); };
        if (form.IsHandleCreated || true) TitleBar.Apply(form.Handle, p.IsDark);
        form.ShowDialog(owner);
    }

    /// <summary>The version history as plain text, newest first.</summary>
    public static string ChangelogText() => string.Join("\n\n", Changelog.Entries.Select(e =>
        $"{e.Version}  ({e.Date})\n" + string.Join("\n", e.Items.Select(i => "• " + i))));
}
