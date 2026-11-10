using System.Reflection;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class RailTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-rail-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }

    private static SideRail RailOf(MainWindow w) =>
        (SideRail)typeof(MainWindow).GetField("_rail", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(w)!;

    [Theory]
    [InlineData("day")]
    [InlineData("cards")]
    [InlineData("feed")]
    [InlineData("tabs")]
    public void TheTodayButtonOfTheRailGoesToTodayInEveryLayout(string layout)
    {
        using var window = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        window.ChooseLayout(layout);
        window.SetDate(new DateOnly(2026, 8, 15));
        Application.DoEvents();
        var today = DateOnly.FromDateTime(DateTime.Today);
        // the item itself, and a real click on its cell
        RailOf(window).Items.First(i => i.Key == "today").Click();
        Application.DoEvents();
        Assert.Equal(today, window.SelectedDate);

        window.SetDate(new DateOnly(2026, 8, 15));
        var rail = RailOf(window);
        var click = typeof(Control).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance)!;
        click.Invoke(rail, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 30, 0) });
        Assert.Equal(today, window.SelectedDate);
    }
}
