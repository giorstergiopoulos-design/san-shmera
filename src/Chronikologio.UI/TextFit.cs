namespace Chronikologio.UI;

/// <summary>Gives a read-only multiline text box a vertical scroll bar exactly when its text is longer than the box (a text box with the bar always on shows a dead bar).</summary>
internal static class TextFit
{
    public static void ScrollWhenLonger(TextBox box)
    {
        void Update()
        {
            if (box.Width < 40 || box.Height < 20) return;
            // measured at the width the box has WITHOUT the bar, whatever its state now, so the bar cannot flip on and off
            var full = box.ClientSize.Width + (box.ScrollBars == ScrollBars.Vertical ? SystemInformation.VerticalScrollBarWidth : 0) - 6;
            var h = TextRenderer.MeasureText(box.Text, box.Font, new Size(Math.Max(20, full), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 4;
            var wanted = h > box.ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
            if (box.ScrollBars != wanted) box.ScrollBars = wanted;
        }
        box.TextChanged += (_, _) => Update();
        box.SizeChanged += (_, _) => Update();
    }
}
