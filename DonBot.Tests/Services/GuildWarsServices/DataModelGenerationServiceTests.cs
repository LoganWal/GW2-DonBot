using DonBot.Services.GuildWarsServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.GuildWarsServices;

public class DataModelGenerationServiceTests
{
    private static DataModelGenerationService NewService() =>
        new(NullLogger<DataModelGenerationService>.Instance, new StubFactory());

    [Fact]
    public void GenerateFromHtml_NoLogDataScript_ReturnsEmptyModelWithUrl()
    {
        var svc = NewService();

        var result = svc.GenerateEliteInsightDataModelFromHtml("<html><body>nothing here</body></html>", "https://b.dps.report/abc");

        Assert.Equal("https://b.dps.report/abc", result.FightEliteInsightDataModel.Url);
        Assert.Null(result.FightEliteInsightDataModel.Players);
    }

    [Fact]
    public void GenerateFromHtml_MalformedScriptTag_ReturnsEmptyModel()
    {
        // missing closing </script>
        var svc = NewService();
        var html = "<script>var _logData = { \"encounterID\": 1234 };";

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/abc");

        Assert.Equal("https://b.dps.report/abc", result.FightEliteInsightDataModel.Url);
    }

    [Fact]
    public void GenerateFromHtml_ScriptWithLogDataButNoOtherSections_ParsesAndDefaultsExtensions()
    {
        // _logData present, no _healingStatsExtension or _barrierStatsExtension
        var html = """
                   <html><script>var _logData = {"url":"placeholder","encounterID":1000};</script></html>
                   """;
        var svc = NewService();

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/abc");

        // URL is overwritten with the input url
        Assert.Equal("https://b.dps.report/abc", result.FightEliteInsightDataModel.Url);
        // Healing/Barrier extensions default to new()
        Assert.NotNull(result.HealingEliteInsightDataModel);
        Assert.NotNull(result.BarrierEliteInsightDataModel);
    }

    [Fact]
    public void GenerateFromHtml_ScriptWithAllThreeSections_PopulatesAllRawFields()
    {
        var html = """
                   <html>
                   <script>
                   var _logData = {"url":"placeholder","encounterID":42};
                   var _healingStatsExtension = {"name":"healing"};
                   var _barrierStatsExtension = {"name":"barrier"};
                   </script>
                   </html>
                   """;
        var svc = NewService();

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/abc");

        Assert.NotNull(result.RawFightData);
        Assert.Contains("encounterID", result.RawFightData);
        Assert.NotNull(result.RawHealingData);
        Assert.Contains("healing", result.RawHealingData);
        Assert.NotNull(result.RawBarrierData);
        Assert.Contains("barrier", result.RawBarrierData);
    }

    [Fact]
    public void GenerateFromHtml_LogDataAcrossMultipleScriptTags_FindsTheRightOne()
    {
        var html = """
                   <html>
                   <script>var unrelated = 1;</script>
                   <script>var _logData = {"encounterID":99};</script>
                   </html>
                   """;
        var svc = NewService();

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/abc");

        Assert.NotNull(result.RawFightData);
        Assert.Contains("99", result.RawFightData);
    }

    [Fact]
    public void GenerateFromHtml_InputUrlOverridesUrlInsideJson()
    {
        var html = """<html><script>var _logData = {"url":"https://stale.url/inside-json","encounterID":1};</script></html>""";
        var svc = NewService();

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/correct");

        Assert.Equal("https://b.dps.report/correct", result.FightEliteInsightDataModel.Url);
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
