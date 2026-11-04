using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class HelpDialogTests : IDisposable
{
    public void Dispose() { Loc.Set("el"); ChronikologioTheme.Set("dark"); }

    private static HelpDialog Open(int width = 860, int height = 680)
    {
        var d = new HelpDialog { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Opacity = 0, ClientSize = new Size(width, height) };
        d.Show();
        Application.DoEvents();
        return d;
    }

    [Fact]
    public void HelpHasTheFourTabsWithCards()
    {
        using var d = Open();
        Assert.Equal(new[] { "Οδηγίες χρήσης", "Ιστορικό εκδόσεων", "Άδεια χρήσης", "Πηγές" }, d.Pages.Select(p => p.Text));
        Assert.All(d.Pages, p => Assert.True(p.CardCount >= 1, p.Text));
        Assert.Equal(Changelog.Entries.Count, d.Pages[1].CardCount);
    }

    [Fact]
    public void TheSourcesPageNamesSaintGrAndTheLicencePageHasTheMitTerms()
    {
        using var d = Open();
        string TextOf(HelpDialog.CardPage p) => string.Join("\n", p.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).Select(c => c.Text));
        Assert.Contains("saint.gr", TextOf(d.Pages[3]));
        Assert.Contains("MIT License", TextOf(d.Pages[2]));
        Assert.DoesNotContain("saint.gr", TextOf(d.Pages[0]));   // the saint.gr credit is only in the sources of Help
    }

    [Theory]
    [InlineData("dark", 860, 680)]
    [InlineData("light", 860, 680)]
    [InlineData("dark", 560, 420)]
    public void CardsAreStackedWithoutOverlapAndFitTheirText(string theme, int width, int height)
    {
        ChronikologioTheme.Set(theme);
        using var d = Open(width, height);
        for (var tab = 0; tab < d.Pages.Count; tab++)
        {
            d.SelectedTab = tab;   // only the page of the chosen tab has its real size
            Application.DoEvents();
            var page = d.Pages[tab];
            page.Arrange();
            var cards = page.Controls.Cast<Control>().OrderBy(c => c.Top).ToList();
            for (var i = 1; i < cards.Count; i++) Assert.True(cards[i].Top >= cards[i - 1].Bottom, $"{page.Text}: cards overlap");
            foreach (var card in cards)
            {
                Assert.True(card.Right <= page.ClientSize.Width, $"{page.Text}: card {card.Left}+{card.Width} wider than the page {page.ClientSize.Width}");
                foreach (Label l in card.Controls)
                {
                    var need = TextRenderer.MeasureText(l.Text, l.Font, new Size(l.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
                    Assert.True(need <= l.Height, $"{page.Text}: text cut off");
                    Assert.True(l.Bottom <= card.Height, $"{page.Text}: label outside its card");
                }
            }
        }
    }

    [Fact]
    public void TheHelpOfTheRailMenuAndF1OpenOnTheRightTab()
    {
        using var d = Open();
        d.SelectedTab = 2;
        Assert.Equal(2, d.SelectedTab);
        d.SelectedTab = 99;
        Assert.Equal(3, d.SelectedTab);
    }

    [Fact]
    public void RenderHelp()
    {
        var dir = Environment.GetEnvironmentVariable("CHRON_RENDER_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            using var d = Open();
            for (var tab = 0; tab < 4; tab++)
            {
                d.SelectedTab = tab;
                Application.DoEvents();
                using var bmp = new Bitmap(d.Width, d.Height);
                d.DrawToBitmap(bmp, new Rectangle(Point.Empty, d.Size));
                bmp.Save(Path.Combine(dir, $"help-{theme}-{tab}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
