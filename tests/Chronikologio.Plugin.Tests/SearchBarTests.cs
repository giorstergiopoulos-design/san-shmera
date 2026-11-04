using System.Reflection;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class SearchBarTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-search-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    private MainWindow Open()
    {
        var w = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        w.Show();
        Application.DoEvents();
        return w;
    }

    [Fact]
    public void TheSearchIsAtTheTopLeftOfTheWindowAboveTheSideRail()
    {
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        // in the first row of the header, on the right, just left of the Today button
        Assert.IsType<DayHeader>(bar.Parent);
        var at = w.PointToClient(bar.PointToScreen(Point.Empty));
        Assert.True(at.Y < 80, "in the top row");
        Assert.True(at.X + bar.Width > w.ClientSize.Width - 130, "at the right, next to the Today button");
        Assert.True(at.X > w.ClientSize.Width / 2, "on the right half");
        var rail = All(w).OfType<SideRail>().Single();
        Assert.DoesNotContain(rail.Items, i => i.Key == "search");
    }

    [Fact]
    public void TheLastItemOfTheRailIsCalledAbout()
    {
        using var w = Open();
        var rail = All(w).OfType<SideRail>().Single();
        Assert.StartsWith("Σχετικά", rail.Items[^1].Tip);
        Assert.Equal("help", rail.Items[^1].Key);
    }

    [Fact]
    public void TypingANameListsItAndChoosingItGoesToItsDay()
    {
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        bar.Search("Νεκταρ");
        Application.DoEvents();
        Assert.True(bar.ListVisible);
        Assert.Contains(bar.Results, r => r.Contains("Νεκτάριος"));
        var index = bar.Results.ToList().FindIndex(r => r.Contains("Νεκτάριος"));
        bar.ChooseResult(index);
        Assert.Equal(11, w.SelectedDate.Month);
        Assert.Equal(9, w.SelectedDate.Day);
        Assert.False(bar.ListVisible);
    }

    [Fact]
    public void SaintsAndEventsAreFoundToo()
    {
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        bar.Search("Τιτανικός");
        Application.DoEvents();
        Assert.Contains(bar.Results, r => r.Contains("Τιτανικ"));
        bar.Search("Δημήτριος");
        Application.DoEvents();
        Assert.Contains(bar.Results, r => r.Contains("Άγιος Δημήτριος"));
    }
}
