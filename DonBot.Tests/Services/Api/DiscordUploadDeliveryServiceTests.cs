using Discord;
using Discord.WebSocket;
using DonBot.Api.Services;
using DonBot.Core.Models;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.Api;

public class DiscordUploadDeliveryServiceTests
{
    [Fact]
    public async Task DeliverAsync_PveDefaultsSendsPrimarySummary()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        var gateway = new FakeGateway([100]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("sent", result.Outcome);
        var sent = Assert.Single(gateway.Sent);
        Assert.Equal(100, sent.ChannelId);
        Assert.NotNull(sent.Embed);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(context.LogUploadDiscordDeliveryReceipt);
        Assert.Equal(DiscordDeliveryMessageKinds.PveSummary, receipt.MessageKind);
        Assert.Equal(DiscordDeliveryReceiptStatuses.Sent, receipt.Status);
        Assert.Equal(5001, receipt.DiscordMessageId);
    }

    [Fact]
    public async Task DeliverAsync_WvwOverrideConsolidatesEnabledMessagesInSelectedChannel()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(
            db,
            wvw: true,
            primaryChannelId: 100,
            advancedChannelId: 101,
            streamChannelId: 102,
            mode: DiscordDeliveryModes.ChannelOverride,
            overrideChannelId: 999);
        var gateway = new FakeGateway([999]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: true));

        Assert.Equal("sent", result.Outcome);
        Assert.Equal(3, result.Sent);
        Assert.All(gateway.Sent, message => Assert.Equal(999, message.ChannelId));
        Assert.Equal(2, gateway.Sent.Count(message => message.Embed is not null));
        Assert.Single(gateway.Sent, message => message.Text == "stream-output");
        await using var context = await db.Factory.CreateDbContextAsync();
        var kinds = await context.LogUploadDiscordDeliveryReceipt
            .Select(receipt => receipt.MessageKind)
            .OrderBy(kind => kind)
            .ToListAsync();
        Assert.Equal(
            new[]
            {
                DiscordDeliveryMessageKinds.WvwAdvanced, DiscordDeliveryMessageKinds.WvwStream,
                DiscordDeliveryMessageKinds.WvwSummary
            },
            kinds);
    }

    [Fact]
    public async Task DeliverAsync_MissingPrimaryDefaultSkipsStandardAndContinuesOtherWvwMessages()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(
            db,
            wvw: true,
            primaryChannelId: null,
            advancedChannelId: 101,
            streamChannelId: 102);
        var gateway = new FakeGateway([101, 102]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: true));

        Assert.Equal("partial", result.Outcome);
        Assert.Equal(2, result.Sent);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new long[] { 101, 102 }, gateway.Sent.Select(message => message.ChannelId).Order().ToArray());
    }

    [Fact]
    public async Task DeliverAsync_DiscordPathClaimSkipsEveryApiMessage()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(
            db,
            wvw: true,
            primaryChannelId: 100,
            advancedChannelId: 101,
            streamChannelId: 102);
        var claimService = new DiscordReportDeliveryClaimService(db.Factory);
        Assert.True(await claimService.TryClaimAsync(
            upload.GuildId,
            upload.DpsReportUrl!,
            DiscordReportDeliveryClaimService.DiscordSource));
        var gateway = new FakeGateway([100, 101, 102]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: true));

        Assert.Equal("skipped", result.Outcome);
        Assert.Equal(3, result.Skipped);
        Assert.Empty(gateway.Sent);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipts = await context.LogUploadDiscordDeliveryReceipt.ToListAsync();
        Assert.Equal(3, receipts.Count);
        Assert.All(receipts, receipt =>
        {
            Assert.Equal(DiscordDeliveryReceiptStatuses.Skipped, receipt.Status);
            Assert.Equal("duplicate_delivery", receipt.FailureCode);
        });
    }

    [Fact]
    public async Task DeliverAsync_RevokedAuthorizationSkipsWithoutSending()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        var gateway = new FakeGateway([]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("skipped", result.Outcome);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(gateway.Sent);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(context.LogUploadDiscordDeliveryReceipt);
        Assert.Equal("authorization_revoked", receipt.FailureCode);
    }

    [Fact]
    public async Task NormalizeInterruptedAsync_MarksSendingReceiptAmbiguous()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            context.LogUploadDiscordDeliveryReceipt.Add(new LogUploadDiscordDeliveryReceipt
            {
                LogUploadId = upload.LogUploadId,
                MessageKind = DiscordDeliveryMessageKinds.PveSummary,
                ResolvedChannelId = 100,
                Status = DiscordDeliveryReceiptStatuses.Sending
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(db, new FakeGateway([100]));

        await service.NormalizeInterruptedAsync();

        await using var verification = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(verification.LogUploadDiscordDeliveryReceipt);
        Assert.Equal(DiscordDeliveryReceiptStatuses.Ambiguous, receipt.Status);
        Assert.Equal("send_interrupted", receipt.FailureCode);
    }

    [Fact]
    public async Task DeliverAsync_AmbiguousReceiptIsNeverSentAgain()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            context.LogUploadDiscordDeliveryReceipt.Add(new LogUploadDiscordDeliveryReceipt
            {
                LogUploadId = upload.LogUploadId,
                MessageKind = DiscordDeliveryMessageKinds.PveSummary,
                ResolvedChannelId = 100,
                Status = DiscordDeliveryReceiptStatuses.Ambiguous
            });
            await context.SaveChangesAsync();
        }

        var gateway = new FakeGateway([100]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("ambiguous", result.Outcome);
        Assert.Equal(1, result.Ambiguous);
        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_SendFailureIsNormalizedAndRedacted()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        var gateway = new FakeGateway([100]) { ThrowOnSend = true };
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("failed", result.Outcome);
        Assert.Equal(1, result.Failed);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(context.LogUploadDiscordDeliveryReceipt);
        Assert.Equal("discord_destination_unavailable", receipt.FailureCode);
        Assert.Null(receipt.DiscordMessageId);
    }

    [Fact]
    public async Task DeliverAsync_UnknownSendFailureIsAmbiguousAndNeverRetried()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        var gateway = new FakeGateway([100]) { ThrowAmbiguousOnSend = true };
        var service = CreateService(db, gateway);

        var first = await service.DeliverAsync(upload, BuildData(wvw: false));
        gateway.ThrowAmbiguousOnSend = false;
        var second = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("ambiguous", first.Outcome);
        Assert.Equal("ambiguous", second.Outcome);
        Assert.Empty(gateway.Sent);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(context.LogUploadDiscordDeliveryReceipt);
        Assert.Equal("discord_send_ambiguous", receipt.FailureCode);
    }

    [Fact]
    public async Task DeliverAsync_PendingReceiptKeepsPersistedDestinationWhenGuildDefaultChanges()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        await using (var context = await db.Factory.CreateDbContextAsync())
        {
            context.LogUploadDiscordDeliveryReceipt.Add(new LogUploadDiscordDeliveryReceipt
            {
                LogUploadId = upload.LogUploadId,
                MessageKind = DiscordDeliveryMessageKinds.PveSummary,
                ResolvedChannelId = 100,
                Status = DiscordDeliveryReceiptStatuses.Pending
            });
            var guild = await context.Guild.SingleAsync();
            guild.LogReportChannelId = 200;
            context.Guild.Update(guild);
            await context.SaveChangesAsync();
        }

        var gateway = new FakeGateway([100, 200]);
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("skipped", result.Outcome);
        Assert.Empty(gateway.Sent);
        await using var verification = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(verification.LogUploadDiscordDeliveryReceipt);
        Assert.Equal(100, receipt.ResolvedChannelId);
        Assert.Equal(DiscordDeliveryReceiptStatuses.Skipped, receipt.Status);
        Assert.Equal("route_revoked", receipt.FailureCode);
    }

    [Fact]
    public async Task DeliverAsync_AuthorizationLookupFailureCreatesTerminalReceipt()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: false, primaryChannelId: 100);
        var gateway = new FakeGateway([100]) { ThrowOnAuthorization = true };
        var service = CreateService(db, gateway);

        var result = await service.DeliverAsync(upload, BuildData(wvw: false));

        Assert.Equal("skipped", result.Outcome);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(gateway.Sent);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(context.LogUploadDiscordDeliveryReceipt);
        Assert.Equal(DiscordDeliveryReceiptStatuses.Skipped, receipt.Status);
        Assert.Equal("authorization_revoked", receipt.FailureCode);
    }

    [Fact]
    public async Task RecordFailureAsync_NoReceiptCreatesFailedPrimaryReceipt()
    {
        using var db = new SqliteTestDb();
        var upload = await SeedAsync(db, wvw: true, primaryChannelId: 100);
        var service = CreateService(db, new FakeGateway([100]));

        var result = await service.RecordFailureAsync(upload.LogUploadId, "delivery_processing_failed");

        Assert.Equal("failed", result.Outcome);
        Assert.Equal(1, result.Failed);
        await using var context = await db.Factory.CreateDbContextAsync();
        var receipt = Assert.Single(context.LogUploadDiscordDeliveryReceipt);
        Assert.Equal(DiscordDeliveryMessageKinds.WvwSummary, receipt.MessageKind);
        Assert.Equal(DiscordDeliveryReceiptStatuses.Failed, receipt.Status);
        Assert.Equal("delivery_processing_failed", receipt.FailureCode);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_DiscordLookupFailuresFailClosed()
    {
        using var db = new SqliteTestDb();
        var gateway = new FakeGateway([100]) { ThrowOnAuthorization = true, ThrowOnDiscovery = true };
        var service = CreateService(db, gateway);
        var guild = new Guild
        {
            GuildId = 42,
            LogReportChannelId = 100,
            MannyUploaderDiscordDeliveryEnabled = true,
            MannyUploaderChannelOverrideEnabled = true
        };

        var result = await service.GetCapabilitiesAsync(guild, 123);

        Assert.True(result.Enabled);
        Assert.False(result.DefaultsAvailable);
        Assert.Empty(result.Channels);
        Assert.True(result.AggregateEnabled);
        Assert.Equal(100, result.MaxAggregateFightLogs);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_AdvancedChannelPreservesExistingDefaultsWithoutAggregateDefaults()
    {
        using var db = new SqliteTestDb();
        var gateway = new FakeGateway([200]);
        var service = CreateService(db, gateway);
        var guild = new Guild
        {
            GuildId = 42, AdvanceLogReportChannelId = 200, MannyUploaderDiscordDeliveryEnabled = true
        };

        var result = await service.GetCapabilitiesAsync(guild, 123);

        Assert.True(result.Enabled);
        Assert.True(result.DefaultsAvailable);
        Assert.True(result.AggregateEnabled);
        Assert.Equal(100, result.MaxAggregateFightLogs);
        Assert.False(result.AggregateDefaultsAvailable);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_DisabledGuildDoesNotAdvertiseAggregateDelivery()
    {
        using var db = new SqliteTestDb();
        var service = CreateService(db, new FakeGateway([100]));
        var guild = new Guild { GuildId = 42, LogReportChannelId = 100 };

        var result = await service.GetCapabilitiesAsync(guild, 123);

        Assert.False(result.Enabled);
        Assert.False(result.AggregateEnabled);
        Assert.Equal(100, result.MaxAggregateFightLogs);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_StreamChannelPreservesExistingDefaultsWithoutAggregateDefaults()
    {
        using var db = new SqliteTestDb();
        var service = CreateService(db, new FakeGateway([300]));
        var guild = new Guild { GuildId = 42, StreamLogChannelId = 300, MannyUploaderDiscordDeliveryEnabled = true };

        var result = await service.GetCapabilitiesAsync(guild, 123);

        Assert.True(result.DefaultsAvailable);
        Assert.False(result.AggregateDefaultsAvailable);
    }

    private static DiscordUploadDeliveryService CreateService(SqliteTestDb db, FakeGateway gateway) =>
        new(
            db.Factory,
            gateway,
            new DiscordReportDeliveryClaimService(db.Factory),
            new FakePveRenderer(),
            new FakeWvwRenderer(),
            NullLogger<DiscordUploadDeliveryService>.Instance);

    private static async Task<LogUpload> SeedAsync(
        SqliteTestDb db,
        bool wvw,
        long? primaryChannelId,
        long? advancedChannelId = null,
        long? streamChannelId = null,
        string mode = DiscordDeliveryModes.GuildDefaults,
        long? overrideChannelId = null)
    {
        await using var context = await db.Factory.CreateDbContextAsync();
        context.Guild.Add(new Guild
        {
            GuildId = 42,
            LogReportChannelId = primaryChannelId,
            AdvanceLogReportChannelId = advancedChannelId,
            StreamLogChannelId = streamChannelId,
            MannyUploaderDiscordDeliveryEnabled = true,
            MannyUploaderChannelOverrideEnabled = true
        });
        var fightLog = new FightLog
        {
            GuildId = 42,
            Url = "https://dps.report/abc",
            FightType = (short)(wvw ? FightTypesEnum.WvW : FightTypesEnum.Cairn),
            FightStart = DateTime.UtcNow,
            FightDurationInMs = 1000
        };
        context.FightLog.Add(fightLog);
        await context.SaveChangesAsync();

        var upload = new LogUpload
        {
            DiscordId = 123,
            GuildId = 42,
            FileName = "abc",
            Status = "delivering",
            SourceType = "url",
            DpsReportUrl = "https://dps.report/abc",
            FightLogId = fightLog.FightLogId,
            DiscordDeliveryMode = mode,
            DiscordDeliveryChannelId = overrideChannelId
        };
        context.LogUpload.Add(upload);
        await context.SaveChangesAsync();
        return upload;
    }

    private static EliteInsightDataModel BuildData(bool wvw) =>
        new(
            new FightEliteInsightDataModel { Wvw = wvw },
            new HealingEliteInsightDataModel(),
            new BarrierEliteInsightDataModel(),
            null,
            null,
            null);

    private sealed class FakeGateway(IEnumerable<long> authorizedChannels) : IDiscordDeliveryGateway
    {
        private readonly HashSet<long> _authorizedChannels = authorizedChannels.ToHashSet();

        public List<SentMessage> Sent { get; } = [];

        public bool ThrowOnSend { get; init; }

        public bool ThrowAmbiguousOnSend { get; set; }

        public bool ThrowOnAuthorization { get; init; }

        public bool ThrowOnDiscovery { get; init; }

        public Task<IReadOnlyList<DiscordAuthorizedChannel>> GetAuthorizedChannelsAsync(long discordId, long guildId,
            CancellationToken ct = default)
        {
            if (ThrowOnDiscovery)
            {
                throw new HttpRequestException("sensitive Discord discovery response");
            }

            return Task.FromResult<IReadOnlyList<DiscordAuthorizedChannel>>(
                _authorizedChannels.Select(channelId => new DiscordAuthorizedChannel(channelId, $"channel-{channelId}"))
                    .ToList());
        }

        public Task<bool> IsAuthorizedChannelAsync(long discordId, long guildId, long channelId,
            CancellationToken ct = default)
        {
            if (ThrowOnAuthorization)
            {
                throw new HttpRequestException("sensitive Discord lookup response");
            }

            return Task.FromResult(_authorizedChannels.Contains(channelId));
        }

        public Task<long> SendMessageAsync(long channelId, string text, Embed? embed, MessageComponent? components,
            CancellationToken ct = default)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("sensitive Discord response");
            }

            if (ThrowAmbiguousOnSend)
            {
                throw new HttpRequestException("unknown send outcome");
            }

            Sent.Add(new SentMessage(channelId, text, embed, components));
            return Task.FromResult(5000L + Sent.Count);
        }
    }

    private sealed record SentMessage(long ChannelId, string Text, Embed? Embed, MessageComponent? Components);

    private sealed class FakePveRenderer : IPvEFightSummaryService
    {
        public Task<(Embed Embed, string? WebAppUrl, long FightLogId)> GenerateSimple(EliteInsightDataModel data,
            long guildId) =>
            throw new NotImplementedException();

        public Task<(Embed Embed, string? WebAppUrl)> RenderSimple(EliteInsightDataModel data, long guildId,
            FightLog fightLog) =>
            Task.FromResult((new EmbedBuilder().WithTitle("PvE").Build(), (string?)"https://donbot/logs/1"));
    }

    private sealed class FakeWvwRenderer : IWvWFightSummaryService
    {
        public Task<(Embed Embed, string? WebAppUrl, long? FightLogId)> Generate(EliteInsightDataModel data,
            bool advancedLog, Guild guild, DiscordSocketClient client) =>
            throw new NotImplementedException();

        public Task<WvWFightSummaryRenderResult> Render(EliteInsightDataModel data, bool advancedLog, Guild guild,
            FightLog? fightLog) =>
            Task.FromResult(new WvWFightSummaryRenderResult(
                new EmbedBuilder().WithTitle(advancedLog ? "Advanced" : "Standard").Build(),
                advancedLog ? null : "https://donbot/logs/1",
                fightLog?.FightLogId,
                "stream-output"));

        public Task<Embed> GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players,
            EmbedBuilder message, long guildId, StatTotals? statTotals = null) =>
            throw new NotImplementedException();
    }
}
