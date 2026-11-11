using System.Net;
using System.Security.Cryptography;
using System.Text;
using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class UpdateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chron-update-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Installer = Encoding.UTF8.GetBytes("pretend installer " + new string('x', 5000));
    private static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    public UpdateTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static string Release(string tag, bool draft = false, bool prerelease = false, string installerUrl = "", string hashUrl = "")
    {
        var baseUrl = $"https://github.com/{UpdateChecker.Repository}/releases/download/{tag}";
        installerUrl = installerUrl.Length > 0 ? installerUrl : $"{baseUrl}/ChronikologioSetup.exe";
        hashUrl = hashUrl.Length > 0 ? hashUrl : $"{baseUrl}/ChronikologioSetup.exe.sha256";
        return $$"""
        {"tag_name":"{{tag}}","draft":{{draft.ToString().ToLowerInvariant()}},"prerelease":{{prerelease.ToString().ToLowerInvariant()}},"html_url":"https://github.com/{{UpdateChecker.Repository}}/releases/tag/{{tag}}","body":"notes",
         "assets":[{"name":"ChronikologioSetup.exe","size":{{Installer.Length}},"browser_download_url":"{{installerUrl}}"},{"name":"ChronikologioSetup.exe.sha256","size":90,"browser_download_url":"{{hashUrl}}"}]}
        """;
    }

    private sealed class Fake : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Reply = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var r = Reply(request);
            r.RequestMessage ??= request;
            return Task.FromResult(r);
        }
    }

    private static HttpResponseMessage Json(string json, string? etag = null)
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (etag is not null) r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        return r;
    }

    private UpdateChecker Checker(Fake fake) => new(new HttpClient(fake), Path.Combine(_dir, "state.json"));
    private static readonly Version Now = new(0, 8, 0);

    [Fact]
    public async Task ANewerReleaseIsOffered()
    {
        var fake = new Fake { Reply = _ => Json(Release("v0.9.0")) };
        var r = await Checker(fake).CheckAsync(Now);
        Assert.Equal(UpdateOutcome.Available, r.Outcome);
        Assert.Equal(new Version(0, 9, 0), r.Update!.Version);
        Assert.EndsWith("ChronikologioSetup.exe.sha256", r.Update.HashUrl);
    }

    [Fact]
    public async Task TheSameOrAnOlderVersionIsNotOffered()
    {
        Assert.Equal(UpdateOutcome.UpToDate, (await Checker(new Fake { Reply = _ => Json(Release("v0.8.0")) }).CheckAsync(new Version(0, 8, 0, 0), true)).Outcome);   // tag 0.8.0 = assembly 0.8.0.0
        Assert.Equal(UpdateOutcome.UpToDate, (await Checker(new Fake { Reply = _ => Json(Release("v0.7.3")) }).CheckAsync(Now, true)).Outcome);
    }

    [Fact]
    public void DraftsAndPreReleasesAreIgnored()
    {
        Assert.Equal(UpdateOutcome.UpToDate, UpdateChecker.Parse(Release("v0.9.0", draft: true), Now).Outcome);
        Assert.Equal(UpdateOutcome.UpToDate, UpdateChecker.Parse(Release("v0.9.0", prerelease: true), Now).Outcome);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"tag_name\":\"hello\"}")]
    [InlineData("{}")]
    public void ABadAnswerIsReportedNotThrown(string json) => Assert.Equal(UpdateOutcome.BadAnswer, UpdateChecker.Parse(json, Now).Outcome);

    [Fact]
    public void AnInstallerFromAnotherPlaceIsNotAccepted()
    {
        Assert.Equal(UpdateOutcome.NoInstaller, UpdateChecker.Parse(Release("v0.9.0", installerUrl: "https://evil.example/ChronikologioSetup.exe"), Now).Outcome);
        Assert.Equal(UpdateOutcome.NoInstaller, UpdateChecker.Parse(Release("v0.9.0", hashUrl: "http://github.com/x/y/releases/download/v1/a.sha256"), Now).Outcome);
        Assert.Equal(UpdateOutcome.NoInstaller, UpdateChecker.Parse(Release("v0.9.0", installerUrl: $"https://github.com/other/repo/releases/download/v0.9.0/ChronikologioSetup.exe"), Now).Outcome);
        Assert.Equal(UpdateOutcome.NoInstaller, UpdateChecker.Parse("{\"tag_name\":\"v0.9.0\",\"assets\":[]}", Now).Outcome);
    }

    [Fact]
    public async Task NoNetworkIsAnAnswerAndTheNextTryMayComeSoon()
    {
        var fake = new Fake { Reply = _ => throw new HttpRequestException("offline") };
        var checker = Checker(fake);
        Assert.Equal(UpdateOutcome.NoNetwork, (await checker.CheckAsync(Now)).Outcome);
        fake.Reply = _ => Json(Release("v0.9.0"));
        Assert.Equal(UpdateOutcome.Available, (await checker.CheckAsync(Now)).Outcome);   // the failed try did not start the 24 hours
    }

    [Fact]
    public async Task ARateLimitedServerIsNoNetworkAndATimeoutToo()
    {
        Assert.Equal(UpdateOutcome.NoNetwork, (await Checker(new Fake { Reply = _ => new HttpResponseMessage(HttpStatusCode.Forbidden) }).CheckAsync(Now)).Outcome);
        Assert.Equal(UpdateOutcome.NoNetwork, (await Checker(new Fake { Reply = _ => throw new TaskCanceledException("timeout") }).CheckAsync(Now)).Outcome);
    }

    [Fact]
    public async Task ItAsksOnlyOnceADayUnlessTheUserAsks()
    {
        var fake = new Fake { Reply = _ => Json(Release("v0.9.0")) };
        var checker = Checker(fake);
        var t0 = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(UpdateOutcome.Available, (await checker.CheckAsync(Now, false, t0)).Outcome);
        Assert.Equal(UpdateOutcome.TooSoon, (await checker.CheckAsync(Now, false, t0.AddHours(3))).Outcome);
        Assert.Equal(1, fake.Calls);
        Assert.Equal(UpdateOutcome.Available, (await checker.CheckAsync(Now, true, t0.AddHours(3))).Outcome);   // by hand: no waiting
        Assert.Equal(UpdateOutcome.Available, (await checker.CheckAsync(Now, false, t0.AddHours(30))).Outcome);
    }

    [Fact]
    public async Task TheEtagGivesTheCachedAnswerWhenNothingChanged()
    {
        var fake = new Fake { Reply = _ => Json(Release("v0.9.0"), "\"abc\"") };
        var checker = Checker(fake);
        await checker.CheckAsync(Now, true);
        fake.Reply = req => req.Headers.TryGetValues("If-None-Match", out var v) && v.Single() == "\"abc\"" ? new HttpResponseMessage(HttpStatusCode.NotModified) : Json("{}");
        var again = await checker.CheckAsync(Now, true);
        Assert.Equal(UpdateOutcome.Available, again.Outcome);
    }

    [Fact]
    public async Task ASkippedVersionIsNotOfferedAgainButAManualCheckShowsIt()
    {
        var fake = new Fake { Reply = _ => Json(Release("v0.9.0")) };
        var checker = Checker(fake);
        checker.Skip("v0.9.0");
        Assert.Equal(UpdateOutcome.Skipped, (await checker.CheckAsync(Now, false, DateTime.UtcNow.AddDays(-3))).Outcome);
        Assert.Equal(UpdateOutcome.Available, (await checker.CheckAsync(Now, true)).Outcome);
    }

    // ---- download and verification

    private static UpdateInfo Info(string tag = "v0.9.0") => new(tag, new Version(0, 9, 0), "page",
        $"https://github.com/{UpdateChecker.Repository}/releases/download/{tag}/ChronikologioSetup.exe",
        $"https://github.com/{UpdateChecker.Repository}/releases/download/{tag}/ChronikologioSetup.exe.sha256", "", Installer.Length);

    private static Fake Server(string hashFile, byte[] installer) => new()
    {
        Reply = req => req.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(hashFile) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) }
    };

    [Fact]
    public async Task AnInstallerWithTheRightHashIsKept()
    {
        var path = await new UpdateInstaller(new HttpClient(Server(Hash(Installer) + " *ChronikologioSetup.exe\n", Installer))).DownloadAsync(Info(), _dir);
        Assert.True(File.Exists(path));
        Assert.Equal(Installer, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".part"));
    }

    [Fact]
    public async Task AnInstallerWithAWrongHashIsDeletedAndNeverRuns()
    {
        var other = Hash(Encoding.UTF8.GetBytes("something else"));
        var ex = await Assert.ThrowsAsync<UpdateException>(() => new UpdateInstaller(new HttpClient(Server(other, Installer))).DownloadAsync(Info(), _dir));
        Assert.Equal(UpdateFailure.HashMismatch, ex.Failure);
        Assert.Empty(Directory.GetFiles(_dir, "ChronikologioSetup*"));
    }

    [Fact]
    public async Task WithoutAReadableHashNothingIsDownloaded()
    {
        var ex = await Assert.ThrowsAsync<UpdateException>(() => new UpdateInstaller(new HttpClient(Server("garbage", Installer))).DownloadAsync(Info(), _dir));
        Assert.Equal(UpdateFailure.NoHash, ex.Failure);
        Assert.Empty(Directory.GetFiles(_dir, "ChronikologioSetup*"));
    }

    [Fact]
    public async Task AnUntrustedAddressIsRefusedBeforeAnyRequest()
    {
        var fake = Server(Hash(Installer), Installer);
        var bad = Info() with { InstallerUrl = "https://example.com/ChronikologioSetup.exe" };
        var ex = await Assert.ThrowsAsync<UpdateException>(() => new UpdateInstaller(new HttpClient(fake)).DownloadAsync(bad, _dir));
        Assert.Equal(UpdateFailure.Untrusted, ex.Failure);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task ARedirectToAnotherHostIsRefused()
    {
        var fake = new Fake
        {
            Reply = req => req.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Hash(Installer)) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer), RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.example/x.exe") }
        };
        var ex = await Assert.ThrowsAsync<UpdateException>(() => new UpdateInstaller(new HttpClient(fake)).DownloadAsync(Info(), _dir));
        Assert.Equal(UpdateFailure.Untrusted, ex.Failure);
        Assert.Empty(Directory.GetFiles(_dir, "ChronikologioSetup*"));
    }

    [Fact]
    public async Task ANetworkErrorWhileDownloadingIsReportedAndLeavesNothing()
    {
        var fake = new Fake { Reply = req => req.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Hash(Installer)) } : throw new HttpRequestException("cut") };
        var ex = await Assert.ThrowsAsync<UpdateException>(() => new UpdateInstaller(new HttpClient(fake)).DownloadAsync(Info(), _dir));
        Assert.Equal(UpdateFailure.Network, ex.Failure);
        Assert.Empty(Directory.GetFiles(_dir, "ChronikologioSetup*"));
    }

    [Theory]
    [InlineData("e2b85a555cfaea30f33e6c44d54ab85f660a76649d7b6732c5174aa02807cfcc *ChronikologioSetup.exe", true)]
    [InlineData("E2B85A555CFAEA30F33E6C44D54AB85F660A76649D7B6732C5174AA02807CFCC", true)]
    [InlineData("tooshort", false)]
    [InlineData("", false)]
    [InlineData("zzb85a555cfaea30f33e6c44d54ab85f660a76649d7b6732c5174aa02807cfcc", false)]
    public void TheHashFileIsReadStrictly(string text, bool ok) => Assert.Equal(ok, UpdateInstaller.ParseHash(text) is not null);

    [Fact]
    public void AnInvalidSignatureStopsTheLaunch()
    {
        var file = Path.Combine(_dir, "ChronikologioSetup.exe");
        File.WriteAllText(file, "x");
        Assert.False(UpdateInstaller.Launch(file, _ => SignatureState.Invalid));   // refused: no process is started
    }

    [Fact]
    public void AnUnsignedFileIsNotTreatedAsSigned()
    {
        var file = Path.Combine(_dir, "plain.exe");
        File.WriteAllText(file, "not a program");
        Assert.NotEqual(SignatureState.Valid, Authenticode.Check(file));
    }

    [Fact]
    public void TagsAndAssemblyVersionsAreComparedAsMajorMinorBuild()
    {
        Assert.True(UpdateChecker.TryParseTag("v0.9.0", out var v));
        Assert.Equal(UpdateChecker.Norm(new Version(0, 9, 0, 0)), UpdateChecker.Norm(v));
        Assert.False(UpdateChecker.TryParseTag("latest", out _));
    }
}
