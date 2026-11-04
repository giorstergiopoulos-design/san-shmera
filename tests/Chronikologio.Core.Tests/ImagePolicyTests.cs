using System.Text.Json;
using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class ImagePolicyTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(new[] { dir ?? throw new DirectoryNotFoundException("repo root not found") }.Concat(parts).ToArray());
    }

    private static readonly DayStore Store = DayStore.Load(RepoFile("data"));
    private static readonly JsonElement Policy = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "images", "policy.json"))).RootElement;

    private static IEnumerable<ImageRef> AllImages() =>
        Store.AllCommemorations.SelectMany(c => c.Images ?? Array.Empty<ImageRef>())
            .Concat(Store.AllEvents.SelectMany(e => e.Images ?? Array.Empty<ImageRef>()));

    private static bool IsAllowedHost(string host) =>
        Policy.GetProperty("allowedHosts").EnumerateArray().Select(h => h.GetString()!)
            .Any(h => host == h || host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void EveryImageHasCreatorLicenceAndAnOfficialSource()
    {
        var licences = Policy.GetProperty("allowedLicenses").EnumerateArray().Select(l => l.GetString()).ToList();
        foreach (var image in AllImages())
        {
            Assert.False(string.IsNullOrWhiteSpace(image.Creator), image.File);
            Assert.False(string.IsNullOrWhiteSpace(image.Caption), image.File);
            Assert.Contains(image.License, licences);
            Assert.True(Uri.TryCreate(image.SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps, image.File);
            Assert.True(IsAllowedHost(uri!.Host), $"{image.File}: {uri.Host} is not an allowed official host");
            Assert.DoesNotContain("wikipedia.org", uri.Host, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryImageFileExistsAndIsSmallEnough()
    {
        var max = Policy.GetProperty("maxBytes").GetInt64();
        foreach (var image in AllImages())
        {
            var path = Store.ImagePath(image);
            Assert.NotNull(path);
            Assert.True(new FileInfo(path!).Length <= max, image.File);
        }
    }

    [Fact]
    public void ImagePathRefusesNamesThatLeaveTheImagesFolder()
    {
        var sneaky = new ImageRef("../../Chronikologio.slnx", "x", "x", "Public Domain", "https://www.nasa.gov/");
        Assert.Null(Store.ImagePath(sneaky));
    }
}
