using DonBot.Core.Services.GuildWars2;

namespace DonBot.Tests.Services.GuildWars2;

public class ReportUrlHelperTests
{
    [Fact]
    public void TryParseReportUrl_DpsReportUrl_BuildsCanonicalAndGetJsonUrls()
    {
        var ok = ReportUrlHelper.TryParseReportUrl("https://dps.report/SgPa-20260626-215714_bone", out var parsed);

        Assert.True(ok);
        Assert.Equal(ReportUrlKind.DpsReport, parsed.Kind);
        Assert.Equal("dps.report", parsed.Host);
        Assert.Equal("SgPa-20260626-215714_bone", parsed.Permalink);
        Assert.Equal("https://dps.report/SgPa-20260626-215714_bone", parsed.CanonicalUrl);
        Assert.Equal("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone", parsed.GetJsonUrl);
    }

    [Fact]
    public void TryParseReportUrl_BDpsReportUrl_CanonicalizesToDpsReport()
    {
        var ok = ReportUrlHelper.TryParseReportUrl("https://b.dps.report/SgPa-20260626-215714_bone", out var parsed);

        Assert.True(ok);
        Assert.Equal("b.dps.report", parsed.Host);
        Assert.Equal("https://dps.report/SgPa-20260626-215714_bone", parsed.CanonicalUrl);
        Assert.Equal("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone", parsed.GetJsonUrl);
    }

    [Fact]
    public void TryParseReportUrl_WvwReportUrl_UsesWvwGetJsonEndpoint()
    {
        var ok = ReportUrlHelper.TryParseReportUrl("https://wvw.report/5PyM-20260627-211812_wvw", out var parsed);

        Assert.True(ok);
        Assert.Equal(ReportUrlKind.WvwReport, parsed.Kind);
        Assert.True(parsed.IsWvw);
        Assert.Equal("https://wvw.report/5PyM-20260627-211812_wvw", parsed.CanonicalUrl);
        Assert.Equal("https://wvw.report/getJson?permalink=5PyM-20260627-211812_wvw", parsed.GetJsonUrl);
    }

    [Fact]
    public void TryParseReportUrl_GetJsonUrl_UsesCanonicalModelUrl()
    {
        var ok = ReportUrlHelper.TryParseReportUrl(
            "https://dps.report/getJson?permalink=SgPa-20260626-215714_bone",
            out var parsed);

        Assert.True(ok);
        Assert.Equal("SgPa-20260626-215714_bone", parsed.Permalink);
        Assert.Equal("https://dps.report/SgPa-20260626-215714_bone", parsed.CanonicalUrl);
        Assert.Equal("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone", parsed.GetJsonUrl);
    }

    [Fact]
    public void TryParseReportUrl_WvwGetJsonUrl_UsesCanonicalModelUrl()
    {
        var ok = ReportUrlHelper.TryParseReportUrl(
            "https://wvw.report/getJson?permalink=5PyM-20260627-211812_wvw",
            out var parsed);

        Assert.True(ok);
        Assert.Equal(ReportUrlKind.WvwReport, parsed.Kind);
        Assert.True(parsed.IsWvw);
        Assert.Equal("5PyM-20260627-211812_wvw", parsed.Permalink);
        Assert.Equal("https://wvw.report/5PyM-20260627-211812_wvw", parsed.CanonicalUrl);
        Assert.Equal("https://wvw.report/getJson?permalink=5PyM-20260627-211812_wvw", parsed.GetJsonUrl);
    }

    [Fact]
    public void CanonicalizeReportUrl_WhenReportUrl_ReturnsCanonicalDisplayUrl()
    {
        var canonical = ReportUrlHelper.CanonicalizeReportUrl("https://dps.report/getJson?permalink=abc");

        Assert.Equal("https://dps.report/abc", canonical);
    }

    [Fact]
    public void TryParseReportUrl_RequireHttps_RejectsHttp()
    {
        var ok = ReportUrlHelper.TryParseReportUrl("http://dps.report/abc", out _, requireHttps: true);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("https://api.dps.report/abc")]
    [InlineData("https://gw2wingman.nevermindcreations.de/log/abc")]
    [InlineData("https://dps.report/")]
    [InlineData("https://wvw.report/")]
    [InlineData("https://dps.report/getJson")]
    [InlineData("https://dps.report/getJson?permalink=")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void TryParseReportUrl_NonReportUrls_ReturnsFalse(string url)
    {
        var ok = ReportUrlHelper.TryParseReportUrl(url, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ExtractReportUrls_FindsReportUrlsAndTrimsTrailingPunctuation()
    {
        var urls = ReportUrlHelper.ExtractReportUrls(
            "first https://b.dps.report/aaa, second https://wvw.report/bbb) api https://api.dps.report/nope");

        Assert.Equal(["https://b.dps.report/aaa", "https://wvw.report/bbb"], urls);
    }

    [Fact]
    public void ExtractReportUrls_DefaultRequiresHttps()
    {
        var urls = ReportUrlHelper.ExtractReportUrls("http://b.dps.report/aaa https://dps.report/bbb");

        Assert.Equal(["https://dps.report/bbb"], urls);
    }

    [Fact]
    public void ExtractReportAndWingmanUrls_RewritesWingmanLogUrlsAfterReportUrls()
    {
        var urls = ReportUrlHelper.ExtractReportAndWingmanUrls(
            "https://gw2wingman.nevermindcreations.de/log/wing https://b.dps.report/aaa");

        Assert.Equal(
            [
                "https://b.dps.report/aaa",
                "https://gw2wingman.nevermindcreations.de/logContent/wing"
            ],
            urls);
    }

    [Fact]
    public void GetLogSource_InvalidUrl_UsesConfiguredFallback()
    {
        Assert.Equal("upload", ReportUrlHelper.GetLogSource("not-a-url", fallback: "upload"));
    }

    [Theory]
    [InlineData("https://b.dps.report/abc-def", "b.dps.report")]
    [InlineData("https://dps.report/xyz", "dps.report")]
    [InlineData("https://wvw.report/123", "wvw.report")]
    [InlineData("https://gw2wingman.nevermindcreations.de/log/abc", "gw2wingman.nevermindcreations.de")]
    [InlineData("https://B.DPS.REPORT/abc", "b.dps.report")]
    public void GetLogSource_ValidAbsoluteUri_ReturnsLowercaseHost(string url, string expectedHost)
    {
        Assert.Equal(expectedHost, ReportUrlHelper.GetLogSource(url));
    }
}
