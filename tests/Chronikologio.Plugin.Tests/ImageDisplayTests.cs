using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

public class ImageDisplayTests
{
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    [Fact]
    public void ThePluginOutputCarriesTheImages()
    {
        Assert.True(Directory.Exists(Path.Combine(DataRoot(), "images", "icons")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(DataRoot(), "images", "icons")));
    }

    [Theory]
    [InlineData(11, 25)]
    [InlineData(11, 30)]
    [InlineData(12, 6)]
    public void WidgetShowsAThumbnailOnDaysWithAnImage(int month, int day)
    {
        var store = DayStore.Load(DataRoot());
        using var panel = new CalendarPanel(store);
        panel.SelectedDate = new DateOnly(2026, month, day);
        Assert.True(panel.HasThumbnail, $"{day}/{month}");
    }

    [Fact]
    public void WidgetShowsNoThumbnailWhenTheDayHasNoImage()
    {
        var store = DayStore.Load(DataRoot());
        using var panel = new CalendarPanel(store);
        // a day without a photograph of a saint or of an event
        var day = Enumerable.Range(0, 365).Select(i => new DateOnly(2026, 1, 1).AddDays(i))
            .First(d => { var v = store.GetDay(d); return !v.Images.Any() && v.Events.All(e => e.Images is null || e.Images.Length == 0); });
        panel.SelectedDate = day;
        Assert.False(panel.HasThumbnail);
    }

    [Fact]
    public void EveryImageFileIsUsedByTheDatabase()
    {
        // the Cyprus calendar has events of its own, so both calendars are read
        var stores = new[] { DayStore.Load(DataRoot(), "gr"), DayStore.Load(DataRoot(), "cy") };
        var used = stores.SelectMany(s => s.AllCommemorations).SelectMany(c => c.Images ?? Array.Empty<ImageRef>())
            .Concat(stores.SelectMany(s => s.AllEvents).SelectMany(e => e.Images ?? Array.Empty<ImageRef>()))
            .Select(i => i.File.Replace('/', Path.DirectorySeparatorChar)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var root = Path.Combine(DataRoot(), "images");
        var onDisk = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp")   // the list of event photographs (eventimages.tsv) is data, not an image
            .Select(f => Path.GetRelativePath(root, f));
        Assert.All(onDisk, f => Assert.Contains(f, used));
    }

    [Fact]
    public void TheLargePictureOfTheDayIsOnlyTheSaintAndNeverAnEventPhotograph()
    {
        var store = DayStore.Load(DataRoot());
        for (var i = 0; i < 366; i++)
        {
            var day = store.GetDay(new DateOnly(2024, 1, 1).AddDays(i));
            var saint = day.SaintImages.ToList();
            Assert.True(saint.Count <= 1, $"{day.Month}/{day.Day}");
            var events = day.Events.SelectMany(e => e.Images ?? Array.Empty<ImageRef>()).Select(x => x.File).ToHashSet();
            Assert.DoesNotContain(saint, x => events.Contains(x.File));
        }
    }
}
