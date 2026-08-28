using Discord;
using Discord.WebSocket;
using DonBot.Api.Services;
using DonBot.Core.Models;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Services.GuildWarsServices;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.Api;

public class AggregateDiscordDeliveryServiceTests
{
    [Fact]
    public async Task DeliverAsync_ValidDefaultRouteSendsOrderedBundleWithFinalLinkOnly()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true, true]);
        var renderer = new FakeMessageGenerationService(2, "https://donbot.example/logs/aggregate?ids=1,2");
        var service = CreateService(db, gateway, renderer);

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        var result = Assert.IsType<AggregateDiscordDeliveryResult>(attempt.Result);
        Assert.Equal(AggregateDiscordDeliveryFailure.None, attempt.Failure);
        Assert.Equal(2, result.FightLogCount);
        Assert.Equal("sent", result.DiscordDelivery.Outcome);
        Assert.Equal(2, result.DiscordDelivery.Sent);
        Assert.Equal(fightIds, renderer.RequestedFightIds);
        Assert.All(gateway.Sent, message => Assert.Equal(500, message.ChannelId));
        Assert.Null(gateway.Sent[0].Components);
        Assert.NotNull(gateway.Sent[1].Components);

        await using var context = await db.Factory.CreateDbContextAsync();
        Assert.Empty(context.LogUpload);
        Assert.Empty(context.FightsReport);
        Assert.Empty(context.DiscordReportDeliveryClaim);
    }

    [Fact]
    public async Task DeliverAsync_ChannelOverrideUsesOneExactAuthorizedTarget()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true, true]);
        var service = CreateService(db, gateway, new FakeMessageGenerationService(3));

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.ChannelOverride,
            900));

        Assert.NotNull(attempt.Result);
        Assert.Equal(3, attempt.Result.DiscordDelivery.Sent);
        Assert.All(gateway.Sent, message => Assert.Equal(900, message.ChannelId));
    }

    [Fact]
    public async Task DeliverAsync_MatchingDifferentAndUnsetGuildProvenanceAreAccepted()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 43, 0], withPlayers: [true, true, true]);
        var gateway = new FakeGateway([true, true]);
        var renderer = new FakeMessageGenerationService(1);
        var service = CreateService(db, gateway, renderer);

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal(AggregateDiscordDeliveryFailure.None, attempt.Failure);
        Assert.Equal(3, attempt.Result?.FightLogCount);
        Assert.Equal("sent", attempt.Result?.DiscordDelivery.Outcome);
        Assert.Equal(fightIds, renderer.RequestedFightIds);
        Assert.Single(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_FightWithoutAggregatePlayerDataReturnsNotReady()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, false]);
        var gateway = new FakeGateway([true]);
        var renderer = new FakeMessageGenerationService(1);
        var service = CreateService(db, gateway, renderer);

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal(AggregateDiscordDeliveryFailure.FightNotReady, attempt.Failure);
        Assert.Equal(0, renderer.CallCount);
        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_MissingFightReturnsNotFoundBeforeRenderingOrSending()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42], withPlayers: [true]);
        fightIds.Add(long.MaxValue);
        var gateway = new FakeGateway([true]);
        var renderer = new FakeMessageGenerationService(1);
        var service = CreateService(db, gateway, renderer);

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal(AggregateDiscordDeliveryFailure.FightNotFound, attempt.Failure);
        Assert.Equal(0, renderer.CallCount);
        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_TerminalAndAmbiguousFailuresDoNotHideLaterMessages()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true, true])
        {
            SendFailures =
            {
                [1] = new InvalidOperationException("private destination detail"),
                [2] = new HttpRequestException("private response detail")
            }
        };
        var service = CreateService(db, gateway, new FakeMessageGenerationService(3));

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.NotNull(attempt.Result);
        Assert.Equal("partial", attempt.Result.DiscordDelivery.Outcome);
        Assert.Equal(1, attempt.Result.DiscordDelivery.Sent);
        Assert.Equal(1, attempt.Result.DiscordDelivery.Failed);
        Assert.Equal(1, attempt.Result.DiscordDelivery.Ambiguous);
        Assert.Equal(3, gateway.SendAttempts);
    }

    [Fact]
    public async Task DeliverAsync_RevokedRouteAfterRenderingFailsWithoutSending()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true, false]);
        var renderer = new FakeMessageGenerationService(1);
        var service = CreateService(db, gateway, renderer);

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal(1, renderer.CallCount);
        Assert.Equal(AggregateDiscordDeliveryFailure.RouteForbidden, attempt.Failure);
        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_DiscordDependencyFailureIsUnavailableRatherThanForbidden()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true])
        {
            AuthorizationStatusOverride = DiscordChannelAuthorizationStatus.Unavailable
        };
        var service = CreateService(db, gateway, new FakeMessageGenerationService(1));

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal(AggregateDiscordDeliveryFailure.DependencyUnavailable, attempt.Failure);
        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_PlayerDataRemovedDuringRenderingFailsBeforeSend()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true]);
        var renderer = new FakeMessageGenerationService(1, onGenerateAsync: async () =>
        {
            await using var context = await db.Factory.CreateDbContextAsync();
            await context.PlayerFightLog
                .Where(player => player.FightLogId == fightIds[1])
                .ExecuteDeleteAsync();
        });
        var service = CreateService(db, gateway, renderer);

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal(AggregateDiscordDeliveryFailure.FightNotReady, attempt.Failure);
        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_OversizedWebLinkIsOmittedWithoutFailingDelivery()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true, true]);
        var service = CreateService(db, gateway, new FakeMessageGenerationService(1, new string('x', 513)));

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
            123,
            42,
            fightIds,
            DiscordDeliveryModes.GuildDefaults,
            null));

        Assert.Equal("sent", attempt.Result?.DiscordDelivery.Outcome);
        Assert.Null(Assert.Single(gateway.Sent).Components);
    }

    [Fact]
    public async Task DeliverAsync_CancellationBeforeValidationSendsNothing()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        var gateway = new FakeGateway([true]);
        var service = CreateService(db, gateway, new FakeMessageGenerationService(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeliverAsync(
            new AggregateDiscordDeliveryRequest(
                123,
                42,
                fightIds,
                DiscordDeliveryModes.GuildDefaults,
                null),
            cancellation.Token));

        Assert.Empty(gateway.Sent);
    }

    [Fact]
    public async Task DeliverAsync_CancellationAfterOneSendMarksCurrentAmbiguousAndRemainingSkipped()
    {
        using var db = new SqliteTestDb();
        var fightIds = await SeedAsync(db, guildIds: [42, 42], withPlayers: [true, true]);
        using var cancellation = new CancellationTokenSource();
        var gateway = new FakeGateway([true, true]) { CancelOnSendAttempt = 1, SendCancellation = cancellation };
        var service = CreateService(db, gateway, new FakeMessageGenerationService(3));

        var attempt = await service.DeliverAsync(new AggregateDiscordDeliveryRequest(
                123,
                42,
                fightIds,
                DiscordDeliveryModes.GuildDefaults,
                null),
            cancellation.Token);

        Assert.Equal(1, attempt.Result?.DiscordDelivery.Sent);
        Assert.Equal(1, attempt.Result?.DiscordDelivery.Ambiguous);
        Assert.Equal(1, attempt.Result?.DiscordDelivery.Skipped);
        Assert.Equal(2, gateway.SendAttempts);
    }

    private static AggregateDiscordDeliveryService CreateService(
        SqliteTestDb db,
        FakeGateway gateway,
        FakeMessageGenerationService renderer) =>
        new(db.Factory, gateway, renderer, NullLogger<AggregateDiscordDeliveryService>.Instance);

    private static async Task<List<long>> SeedAsync(
        SqliteTestDb db,
        IReadOnlyList<long> guildIds,
        IReadOnlyList<bool> withPlayers)
    {
        await using var context = await db.Factory.CreateDbContextAsync();
        context.Guild.AddRange(
            new Guild
            {
                GuildId = 42,
                LogReportChannelId = 500,
                MannyUploaderDiscordDeliveryEnabled = true,
                MannyUploaderChannelOverrideEnabled = true
            },
            new Guild { GuildId = 43 });

        var fights = guildIds.Select((guildId, index) => new FightLog
        {
            GuildId = guildId,
            Url = $"https://dps.report/{index + 1}",
            FightStart = DateTime.UtcNow.AddMinutes(index),
            FightDurationInMs = 1000,
            FightType = 1
        }).ToList();
        context.FightLog.AddRange(fights);
        await context.SaveChangesAsync();

        for (var index = 0; index < fights.Count; index++)
        {
            if (withPlayers[index])
            {
                context.PlayerFightLog.Add(new PlayerFightLog
                {
                    FightLogId = fights[index].FightLogId, GuildWarsAccountName = $"Player.{index + 1}"
                });
            }
        }

        await context.SaveChangesAsync();
        return fights.Select(fight => fight.FightLogId).ToList();
    }

    private sealed class FakeGateway(IReadOnlyList<bool> authorizations) : IDiscordDeliveryGateway
    {
        private int _authorizationIndex;

        public Dictionary<int, Exception> SendFailures { get; } = [];

        public List<SentMessage> Sent { get; } = [];

        public int SendAttempts { get; private set; }

        public DiscordChannelAuthorizationStatus? AuthorizationStatusOverride { get; init; }

        public int? CancelOnSendAttempt { get; init; }

        public CancellationTokenSource? SendCancellation { get; init; }

        public Task<IReadOnlyList<DiscordAuthorizedChannel>> GetAuthorizedChannelsAsync(
            long discordId,
            long guildId,
            CancellationToken ct = default) => throw new NotImplementedException();

        public Task<bool> IsAuthorizedChannelAsync(
            long discordId,
            long guildId,
            long channelId,
            CancellationToken ct = default)
        {
            var result = authorizations[Math.Min(_authorizationIndex, authorizations.Count - 1)];
            _authorizationIndex++;
            return Task.FromResult(result);
        }

        public Task<DiscordChannelAuthorizationResult> AuthorizeChannelAsync(
            long discordId,
            long guildId,
            long channelId,
            CancellationToken ct = default)
        {
            if (AuthorizationStatusOverride is { } status)
            {
                return Task.FromResult(new DiscordChannelAuthorizationResult(status));
            }

            return IsAuthorizedChannelAsync(discordId, guildId, channelId, ct).ContinueWith(
                task => new DiscordChannelAuthorizationResult(
                    task.Result
                        ? DiscordChannelAuthorizationStatus.Authorized
                        : DiscordChannelAuthorizationStatus.Forbidden),
                ct);
        }

        public Task<long> SendMessageAsync(
            long channelId,
            string text,
            Embed? embed,
            MessageComponent? components,
            CancellationToken ct = default)
        {
            var attempt = SendAttempts++;
            if (attempt == CancelOnSendAttempt && SendCancellation is not null)
            {
                SendCancellation.Cancel();
                throw new OperationCanceledException(ct);
            }

            if (SendFailures.TryGetValue(attempt, out var failure))
            {
                throw failure;
            }

            Sent.Add(new SentMessage(channelId, embed, components));
            return Task.FromResult(1000L + attempt);
        }
    }

    private sealed record SentMessage(long ChannelId, Embed? Embed, MessageComponent? Components);

    private sealed class FakeMessageGenerationService(
        int messageCount,
        string? webAppUrl = null,
        Func<Task>? onGenerateAsync = null)
        : IMessageGenerationService
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<long>? RequestedFightIds { get; private set; }

        public async Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateRaidReplyReport(
            List<long> fightLogIds,
            long guildId)
        {
            CallCount++;
            RequestedFightIds = fightLogIds;
            if (onGenerateAsync is not null)
            {
                await onGenerateAsync();
            }

            var messages = Enumerable.Range(1, messageCount)
                .Select(index => new EmbedBuilder().WithTitle($"Message {index}").Build())
                .ToList();
            return (messages, webAppUrl);
        }

        public Task<(Embed Embed, string? WebAppUrl, long? FightLogId)> GenerateWvWFightSummary(
            EliteInsightDataModel data,
            bool advancedLog,
            Guild guild,
            DiscordSocketClient client) => throw new NotImplementedException();

        public Task<(Embed Embed, string? WebAppUrl, long FightLogId)> GeneratePvEFightSummary(
            EliteInsightDataModel data,
            long guildId) => throw new NotImplementedException();

        public Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateRaidReport(
            FightsReport fightsReport,
            long guildId) => throw new NotImplementedException();

        public Task<Embed> GenerateRaidAlert(long guildId) => throw new NotImplementedException();
    }
}
