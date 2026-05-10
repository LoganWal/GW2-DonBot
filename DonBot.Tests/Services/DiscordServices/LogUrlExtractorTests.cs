using DonBot.Services.DiscordServices;

namespace DonBot.Tests.Services.DiscordServices;

public class LogUrlExtractorTests
{
    [Fact]
    public void ExtractFromText_EmptyContent_ReturnsEmpty()
    {
        Assert.Empty(LogUrlExtractor.ExtractFromText(""));
    }

    [Fact]
    public void ExtractFromText_NoUrls_ReturnsEmpty()
    {
        Assert.Empty(LogUrlExtractor.ExtractFromText("just a chat message"));
    }

    [Theory]
    [InlineData("https://b.dps.report/abc-def")]
    [InlineData("https://dps.report/xyz-123")]
    [InlineData("https://wvw.report/foo-bar")]
    public void ExtractFromText_DpsReportSubdomain_Extracted(string url)
    {
        var urls = LogUrlExtractor.ExtractFromText($"check this out {url} thanks");
        Assert.Single(urls);
        Assert.Equal(url, urls[0]);
    }

    [Fact]
    public void ExtractFromText_MultipleDpsReportUrls_AllExtracted()
    {
        var content = "first https://b.dps.report/aaa second https://wvw.report/bbb done";
        var urls = LogUrlExtractor.ExtractFromText(content);
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://b.dps.report/aaa", urls);
        Assert.Contains("https://wvw.report/bbb", urls);
    }

    [Fact]
    public void ExtractFromText_WingmanLogUrl_RewrittenToLogContentPath()
    {
        var urls = LogUrlExtractor.ExtractFromText("https://gw2wingman.nevermindcreations.de/log/abc-123");
        Assert.Single(urls);
        Assert.Equal("https://gw2wingman.nevermindcreations.de/logContent/abc-123", urls[0]);
    }

    [Fact]
    public void ExtractFromText_MixedDpsReportAndWingman_BothPresent()
    {
        var content = "https://b.dps.report/aaa https://gw2wingman.nevermindcreations.de/log/bbb";
        var urls = LogUrlExtractor.ExtractFromText(content);
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://b.dps.report/aaa", urls);
        Assert.Contains("https://gw2wingman.nevermindcreations.de/logContent/bbb", urls);
    }

    [Fact]
    public void ExtractFromText_HttpNotHttps_NotMatched()
    {
        // both regexes require https; plain http is rejected
        Assert.Empty(LogUrlExtractor.ExtractFromText("http://b.dps.report/aaa"));
    }

    [Fact]
    public void ExtractFromText_DpsReportEmbeddedInSentence_StopsAtFirstWhitespace()
    {
        var urls = LogUrlExtractor.ExtractFromText("see https://b.dps.report/abc and more");
        Assert.Equal(["https://b.dps.report/abc"], urls);
    }

    [Fact]
    public void ExtractFromText_UnknownDpsReportSubdomain_NotMatched()
    {
        // only b.dps, dps, wvw subdomains are extracted
        Assert.Empty(LogUrlExtractor.ExtractFromText("https://api.dps.report/abc"));
    }

    [Fact]
    public void ExtractFromText_WingmanLogContentUrl_NotMatched()
    {
        // the regex requires /log/ exactly; /logContent/ doesn't match because there's no
        // trailing slash after /log
        Assert.Empty(LogUrlExtractor.ExtractFromText("https://gw2wingman.nevermindcreations.de/logContent/abc"));
    }
}
