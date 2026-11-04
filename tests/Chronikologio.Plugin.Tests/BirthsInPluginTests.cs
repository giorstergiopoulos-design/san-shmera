using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

public class BirthsInPluginTests
{
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    [Fact]
    public void ThePluginOutputCarriesTheBirthsFile() =>
        Assert.True(File.Exists(Path.Combine(DataRoot(), "births", "births.tsv")));

    [Fact]
    public void WidgetTextShowsWhoWasBornOnTheChosenDay()
    {
        var store = DayStore.Load(DataRoot());
        using var panel = new CalendarPanel(store);
        panel.SelectedDate = new DateOnly(2026, 3, 14);
        Assert.Contains("Αϊνστάιν", panel.ShortTextShown);
    }
}
