using DonBot.Services.GuildWarsServices;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace DonBot.Tests.Services.GuildWarsServices;

public class DataModelGenerationServiceTests
{
    private static readonly Lazy<string> UraFixture = new(() => ReadFixtureFile("UBVU-20260628-004920_ura.json"));
    private static readonly Lazy<string> WvwFixture = new(() => ReadFixtureFile("5PyM-20260627-211812_wvw.json"));
    private static readonly Lazy<string> BoneskinnerFixture = new(() => ReadFixtureFile("SgPa-20260626-215714_bone.json"));

    private static DataModelGenerationService NewService(HttpMessageHandler? handler = null) =>
        new(NullLogger<DataModelGenerationService>.Instance, new StubFactory(handler));

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
        var svc = NewService();
        var html = "<script>var _logData = { \"encounterID\": 1234 };";

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/abc");

        Assert.Equal("https://b.dps.report/abc", result.FightEliteInsightDataModel.Url);
    }

    [Fact]
    public void GenerateFromHtml_ScriptWithLogDataButNoOtherSections_ParsesAndDefaultsExtensions()
    {
        var html = """
                   <html><script>var _logData = {"url":"placeholder","encounterID":1000};</script></html>
                   """;
        var svc = NewService();

        var result = svc.GenerateEliteInsightDataModelFromHtml(html, "https://b.dps.report/abc");

        Assert.Equal("https://b.dps.report/abc", result.FightEliteInsightDataModel.Url);
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

    [Fact]
    public void GenerateFromJson_UraFixture_MapsHealingAndBarrierFromGetJsonPlayers()
    {
        var svc = NewService();
        var json = ReadFixture("UBVU-20260628-004920_ura.json");

        var result = svc.GenerateEliteInsightDataModelFromJson(json, "https://dps.report/UBVU-20260628-004920_ura");

        Assert.Equal("Ura, the Steamshrieker", result.FightEliteInsightDataModel.LogName);
        Assert.Equal(10, result.FightEliteInsightDataModel.Players?.Count);
        Assert.Equal(21, result.FightEliteInsightDataModel.Phases?.Count);
        Assert.Equal(13_451, result.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets[0].Sum(target => target[0]));
        Assert.Equal(475_971, result.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets[3].Sum(target => target[0]));
        Assert.Equal(47_915, result.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats[0][0]);
        Assert.Equal(80_138, result.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats[3][0]);
        Assert.Equal(5_160_435, result.FightEliteInsightDataModel.Phases![0].DpsStats![0][0]);
        Assert.Equal(5_076_109, result.FightEliteInsightDataModel.Phases[0].DpsStatsTargets![0][0][0]);
        Assert.Contains(1122, result.FightEliteInsightDataModel.Boons);
    }

    [Fact]
    public void GenerateFromJson_UraFixture_PlayerServiceReadsNormalizedStats()
    {
        var svc = NewService();
        var json = ReadFixture("UBVU-20260628-004920_ura.json");
        var data = svc.GenerateEliteInsightDataModelFromJson(json, "https://dps.report/UBVU-20260628-004920_ura");

        var players = new PlayerService(null!).GetGw2Players(data, data.FightEliteInsightDataModel.Phases!.First());
        var player = players.Single(player => player.AccountName == "WalmsLo.8437");

        Assert.Equal(5_104_237, player.Damage);
        Assert.Equal(13_451, player.Healing);
        Assert.Equal(47_915, player.BarrierGenerated);
        Assert.Equal(14, player.Cleanses);
        Assert.Equal(9, player.Strips);
        Assert.Equal(0.607, player.StabOnGroup, 3);
        Assert.Equal(0.010, player.StabOffGroup, 3);
        Assert.Equal(76.073, player.AlacGenGroup, 3);
        Assert.Equal(99.303, player.TotalQuick, 3);
        Assert.Equal(98.711, player.TotalAlac, 3);
    }

    [Fact]
    public void GenerateFromJson_UraFixture_PlayerServiceReadsRequestedPhaseExtensionStats()
    {
        var svc = NewService();
        var json = ReadFixture("UBVU-20260628-004920_ura.json");
        var data = svc.GenerateEliteInsightDataModelFromJson(json, "https://dps.report/UBVU-20260628-004920_ura");

        var players = new PlayerService(null!).GetGw2Players(data, data.FightEliteInsightDataModel.Phases![1]);
        var player = players.Single(player => player.AccountName == "WalmsLo.8437");

        Assert.Equal(1_624_562, player.Damage);
        Assert.Equal(5_940, player.Healing);
        Assert.Equal(15_540, player.BarrierGenerated);
        Assert.Equal(5, player.Cleanses);
    }

    [Fact]
    public void GenerateFromJson_WvwFixture_MapsExtensionStatsAndEnemyDamage()
    {
        var svc = NewService();
        var json = ReadFixture("5PyM-20260627-211812_wvw.json");

        var result = svc.GenerateEliteInsightDataModelFromJson(json, "https://wvw.report/5PyM-20260627-211812_wvw");

        Assert.True(result.FightEliteInsightDataModel.Wvw);
        Assert.Equal(41, result.FightEliteInsightDataModel.Players?.Count);
        Assert.Single(result.FightEliteInsightDataModel.Phases!);
        Assert.Equal(197_073, result.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets[2].Sum(target => target[0]));
        Assert.Equal(324_375, result.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets[17].Sum(target => target[0]));
        Assert.Equal(2_429, result.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats[2][0]);
        Assert.Equal(71_587, result.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats[29][0]);

        var enemyTarget = result.FightEliteInsightDataModel.Targets!.First(target => target.Name != "Dummy PvP Agent");
        Assert.True(enemyTarget.Details?.DmgDistributions?.FirstOrDefault()?.TotalDamage > 0);
    }

    [Fact]
    public void GenerateFromJson_WvwFixture_PlayerServiceReadsExtensionStats()
    {
        var svc = NewService();
        var json = ReadFixture("5PyM-20260627-211812_wvw.json");
        var data = svc.GenerateEliteInsightDataModelFromJson(json, "https://wvw.report/5PyM-20260627-211812_wvw");

        var players = new PlayerService(null!).GetGw2Players(data, data.FightEliteInsightDataModel.Phases!.First());
        var player = players.Single(player => player.AccountName == "Diablyrie.8549");

        Assert.Equal(4_887, player.Damage);
        Assert.Equal(197_073, player.Healing);
        Assert.Equal(2_429, player.BarrierGenerated);
        Assert.Equal(144, player.Cleanses);
        Assert.Equal(1, player.Strips);
    }

    [Fact]
    public void GenerateFromJson_NoHealingFixture_LeavesExtensionModelsEmpty()
    {
        var svc = NewService();
        var json = ReadFixture("SgPa-20260626-215714_bone.json");

        var result = svc.GenerateEliteInsightDataModelFromJson(json, "https://dps.report/SgPa-20260626-215714_bone");

        Assert.Equal("Boneskinner", result.FightEliteInsightDataModel.LogName);
        Assert.Equal(10, result.FightEliteInsightDataModel.Players?.Count);
        Assert.Equal(3, result.FightEliteInsightDataModel.Phases?.Count);
        Assert.Empty(result.HealingEliteInsightDataModel.HealingPhases);
        Assert.Empty(result.BarrierEliteInsightDataModel.BarrierPhases);
        Assert.Null(result.RawHealingData);
        Assert.Null(result.RawBarrierData);
    }

    [Fact]
    public async Task GenerateFromUrl_DpsReportUrl_FetchesGetJsonEndpoint()
    {
        var json = ReadFixture("SgPa-20260626-215714_bone.json");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });
        var svc = NewService(handler);

        var result = await svc.GenerateEliteInsightDataModelFromUrl("https://dps.report/SgPa-20260626-215714_bone");

        Assert.Equal("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone", handler.Requests.Single().ToString());
        Assert.Equal("https://dps.report/SgPa-20260626-215714_bone", result.FightEliteInsightDataModel.Url);
        Assert.Equal("Boneskinner", result.FightEliteInsightDataModel.LogName);
    }

    [Fact]
    public async Task GenerateFromUrl_BDpsReportUrl_FetchesDpsGetJsonEndpoint()
    {
        var json = ReadFixture("SgPa-20260626-215714_bone.json");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });
        var svc = NewService(handler);

        var result = await svc.GenerateEliteInsightDataModelFromUrl("https://b.dps.report/SgPa-20260626-215714_bone");

        Assert.Equal("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone", handler.Requests.Single().ToString());
        Assert.Equal("https://dps.report/SgPa-20260626-215714_bone", result.FightEliteInsightDataModel.Url);
    }

    [Fact]
    public async Task GenerateFromUrl_GetJsonUrl_UsesEndpointAndCanonicalModelUrl()
    {
        var json = ReadFixture("SgPa-20260626-215714_bone.json");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });
        var svc = NewService(handler);

        var result = await svc.GenerateEliteInsightDataModelFromUrl("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone");

        Assert.Equal("https://dps.report/getJson?permalink=SgPa-20260626-215714_bone", handler.Requests.Single().ToString());
        Assert.Equal("https://dps.report/SgPa-20260626-215714_bone", result.FightEliteInsightDataModel.Url);
    }

    [Fact]
    public async Task GenerateFromUrl_GetJsonParseFailure_RetriesRequest()
    {
        var attempts = 0;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(attempts++ == 0
                ? "not json"
                : """{"fightName":"Retry Fight","players":[],"targets":[],"phases":[]}""")
        });
        var svc = NewService(handler);

        var result = await svc.GenerateEliteInsightDataModelFromUrl("https://dps.report/retry-test");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Retry Fight", result.FightEliteInsightDataModel.LogName);
    }

    [Fact]
    public async Task GenerateFromUrl_GetJsonErrorPayload_RetriesRequest()
    {
        var attempts = 0;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(attempts++ == 0
                ? """{"error":"bad permalink","permalink":"retry-test"}"""
                : """{"fightName":"Retry Fight","players":[],"targets":[],"phases":[]}""")
        });
        var svc = NewService(handler);

        var result = await svc.GenerateEliteInsightDataModelFromUrl("https://dps.report/retry-test");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Retry Fight", result.FightEliteInsightDataModel.LogName);
    }

    [Fact]
    public async Task GenerateFromUrl_WvwReportUrl_FetchesWvwGetJsonEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"fightName":"Detailed WvW","players":[],"targets":[],"phases":[]}""")
        });
        var svc = NewService(handler);

        var result = await svc.GenerateEliteInsightDataModelFromUrl("https://wvw.report/5PyM-20260627-211812_wvw");

        Assert.Equal("https://wvw.report/getJson?permalink=5PyM-20260627-211812_wvw", handler.Requests.Single().ToString());
        Assert.True(result.FightEliteInsightDataModel.Wvw);
    }

    private static string ReadFixture(string fileName)
    {
        return fileName switch
        {
            "UBVU-20260628-004920_ura.json" => UraFixture.Value,
            "5PyM-20260627-211812_wvw.json" => WvwFixture.Value,
            "SgPa-20260626-215714_bone.json" => BoneskinnerFixture.Value,
            _ => ReadFixtureFile(fileName)
        };
    }

    private static string ReadFixtureFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "DonBot.Tests")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new DirectoryNotFoundException("Could not locate repository root for dps.report fixtures.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, "DonBot.Tests", "Fixtures", "DpsReport", "GetJson", fileName));
    }

    private sealed class StubFactory(HttpMessageHandler? handler = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }
}
