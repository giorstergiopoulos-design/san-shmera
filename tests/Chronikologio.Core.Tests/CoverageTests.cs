using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class CoverageTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public void EveryMonthHasSaintsAndHistory(int month)
    {
        var days = Enumerable.Range(1, DateTime.DaysInMonth(2025, month)).Select(d => Store.GetDay(month, d)).ToList();
        Assert.Contains(days, d => d.Commemorations.Count > 0);
        Assert.Contains(days, d => d.Events.Count > 0);
    }

    [Fact]
    public void NoRecordSitsOnAnImpossibleDate()
    {
        foreach (var file in Directory.EnumerateFiles(FindData(), "*.json", SearchOption.AllDirectories))
        {
            var mf = System.Text.Json.JsonSerializer.Deserialize<MonthFile>(File.ReadAllText(file), DayStore.Json)!;
            Assert.All(mf.Days, d => Assert.InRange(d.Day, 1, DateTime.DaysInMonth(2024, mf.Month)));
            Assert.Equal(mf.Month, int.Parse(Path.GetFileNameWithoutExtension(file)));
        }
    }
}
