using System.Linq.Expressions;
using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.GuildWars2;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Configuration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class RaidReportFooterTests
{
    private const long GuildId = 1;

    [Fact]
    public async Task Generate_PvEPath_TopAggregateEmbedsHaveDistinctFooters()
    {
        var footer = new SequenceFooterService();
        var report = MakeReport();
        var fights = MakeFights(FightTypesEnum.Cairn, count: 3, report);
        var playerFights = MakePlayerFights(fights);
        var service = BuildService(footer, fights, playerFights);

        var (embeds, _) = await service.Generate(report, GuildId);

        Assert.NotNull(embeds);
        Assert.True(embeds!.Count >= 3, $"Expected >= 3 embeds for PvE path, got {embeds.Count}");

        // First three embeds are the PvE top aggregate (Fights / Player / Survivability);
        // each must have its own distinct footer quote.
        var topFooters = embeds.Take(3).Select(e => e.Footer?.Text).ToList();
        Assert.Equal(3, topFooters.Distinct().Count());
    }

    [Fact]
    public async Task GenerateSimpleReply_LooksUpFightsByFightLogId_NotByUrl()
    {
        // Regression: when a user posts a log whose content is a duplicate of a
        // previously-uploaded fight (different URL, same fight content), the dedupe
        // path reuses the existing FightLog row and the new URL is never stored. The
        // raid reply report must still include that fight, so it has to look up by
        // FightLogId (the resolved id from the summary call), not by the URL the
        // user posted in Discord.
        var footer = new SequenceFooterService();
        var report = MakeReport();
        var fights = MakeFights(FightTypesEnum.Cairn, count: 2, report);
        // Simulate: original uploader posted "/originalA" and "/originalB"; the user
        // who triggered this reply posted "/aliasA" and "/aliasB" - URLs the DB has
        // never seen. The id-based lookup must still find both fights.
        var playerFights = MakePlayerFights(fights);
        var service = BuildService(footer, fights, playerFights);

        var (embeds, _) = await service.GenerateSimpleReply(fights.Select(f => f.FightLogId).ToList(), GuildId);

        Assert.NotNull(embeds);
        Assert.NotEmpty(embeds!);
    }

    [Fact]
    public async Task Generate_WvWPath_TopAggregateEmbedsHaveDistinctFooters()
    {
        var footer = new SequenceFooterService();
        var report = MakeReport();
        var fights = MakeFights(FightTypesEnum.WvW, count: 3, report);
        var playerFights = MakePlayerFights(fights);
        var service = BuildService(footer, fights, playerFights);

        var (embeds, _) = await service.Generate(report, GuildId);

        Assert.NotNull(embeds);
        Assert.Equal(2, embeds!.Count);
        Assert.NotEqual(embeds[0].Footer?.Text, embeds[1].Footer?.Text);
    }

    private static RaidReportService BuildService(
        IFooterService footer,
        List<FightLog> fights,
        List<PlayerFightLog> playerFights)
    {
        var entityService = new FakeEntityService(fights, playerFights);
        var wvw = new FakeWvWSummaryService(footer);
        var config = new ConfigurationBuilder().Build();
        return new RaidReportService(entityService, footer, wvw, config);
    }

    private static FightsReport MakeReport() => new()
    {
        FightsReportId = 1,
        GuildId = GuildId,
        FightsStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FightsEnd = new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc),
    };

    private static List<FightLog> MakeFights(FightTypesEnum type, int count, FightsReport report) =>
        Enumerable.Range(0, count).Select(i => new FightLog
        {
            FightLogId = i + 1,
            GuildId = GuildId,
            Url = $"https://example.com/{i}",
            FightType = (short)type,
            FightStart = report.FightsStart.AddMinutes(i * 10),
            FightDurationInMs = 60_000,
            IsSuccess = true,
            FightPercent = 100,
            FightMode = 0,
        }).ToList();

    private static List<PlayerFightLog> MakePlayerFights(List<FightLog> fights) =>
        fights.Select((f, i) => new PlayerFightLog
        {
            PlayerFightLogId = i + 1,
            FightLogId = f.FightLogId,
            GuildWarsAccountName = "Alice.1234",
            SubGroup = 1,
            Damage = 1000,
        }).ToList();
}

internal sealed class SequenceFooterService : IFooterService
{
    private int _count;

    public Task<string> Generate(long guildId)
    {
        var n = Interlocked.Increment(ref _count);
        return Task.FromResult($"Q{n}");
    }

    public void AddInviteLink(EmbedBuilder builder)
    {
    }
}

internal sealed class FakeWvWSummaryService(IFooterService footerService) : IWvWFightSummaryService
{
    public Task<(Embed Embed, string? WebAppUrl, long? FightLogId)> Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client)
        => throw new NotImplementedException();

    public async Task<Embed> GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players, EmbedBuilder message, long guildId, StatTotals? statTotals = null)
    {
        // Mirror real WvWFightSummaryService: footer is set here, overriding any caller-set footer.
        message.Footer = new EmbedFooterBuilder
        {
            Text = await footerService.Generate(guildId),
            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
        };
        return message.Build();
    }
}

internal sealed class FakeEntityService(List<FightLog> fightLogs, List<PlayerFightLog> playerFightLogs) : IEntityService
{
    public IDatabaseUpdateService<FightLog> FightLog { get; } = new FakeRepo<FightLog>(fightLogs);
    public IDatabaseUpdateService<PlayerFightLog> PlayerFightLog { get; } = new FakeRepo<PlayerFightLog>(playerFightLogs);
    public IDatabaseUpdateService<PlayerFightLogMechanic> PlayerFightLogMechanic { get; } = new FakeRepo<PlayerFightLogMechanic>([]);

    public IDatabaseUpdateService<Account> Account => throw new NotImplementedException();
    public IDatabaseUpdateService<FightLogRawData> FightLogRawData => throw new NotImplementedException();
    public IDatabaseUpdateService<FightsReport> FightsReport => throw new NotImplementedException();
    public IDatabaseUpdateService<Guild> Guild => throw new NotImplementedException();
    public IDatabaseUpdateService<GuildQuote> GuildQuote => throw new NotImplementedException();
    public IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid => throw new NotImplementedException();
    public IDatabaseUpdateService<Raffle> Raffle => throw new NotImplementedException();
    public IDatabaseUpdateService<ScheduledEvent> ScheduledEvent => throw new NotImplementedException();
    public IDatabaseUpdateService<RotationAnomaly> RotationAnomaly => throw new NotImplementedException();
}

internal sealed class FakeRepo<T>(List<T> items) : IDatabaseUpdateService<T> where T : class
{
    public Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate)
        => Task.FromResult(items.AsQueryable().Where(predicate).ToList());

    public Task<List<T>> GetAllAsync() => Task.FromResult(items.ToList());

    public Task AddAsync(T entity) => throw new NotImplementedException();
    public Task AddRangeAsync(List<T> entity) => throw new NotImplementedException();
    public Task UpdateAsync(T entity) => throw new NotImplementedException();
    public Task UpdateRangeAsync(List<T> entity) => throw new NotImplementedException();
    public Task DeleteAsync(T entity) => throw new NotImplementedException();
    public Task DeleteRangeAsync(List<T> entity) => throw new NotImplementedException();
    public Task<bool> IfAnyAsync(Expression<Func<T, bool>> predicate) => throw new NotImplementedException();
    public Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate) => throw new NotImplementedException();
}
