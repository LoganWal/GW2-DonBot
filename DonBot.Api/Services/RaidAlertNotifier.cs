using Discord;
using Discord.Rest;
using DonBot.Models.Entities;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Api.Services;

public interface IRaidNotifier
{
    Task PostRaidStartedAsync(long guildId, CancellationToken ct = default);
    Task PostRaidEndedAsync(FightsReport closedReport, CancellationToken ct = default);
}

// Mirrors the Discord slash-command side effects from RaidCommandCommandService:
//   start → @everyone alert in RaidAlertChannelId
//   stop  → raid report embeds (+ View on DonBot / Best Times buttons) in LogReportChannelId
// Both are best-effort: failures are logged but never throw, so a misconfigured channel
// or Discord outage can't undo a successful DB-side raid open/close.
public sealed class RaidNotifier(
    IEntityService entityService,
    IMessageGenerationService messageGeneration,
    DiscordRestClientProvider clientProvider,
    IConfiguration configuration,
    ILogger<RaidNotifier> logger) : IRaidNotifier
{
    public async Task PostRaidStartedAsync(long guildId, CancellationToken ct = default)
    {
        try
        {
            var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == guildId);
            if (guild == null || !guild.RaidAlertEnabled || guild.RaidAlertChannelId == null)
            {
                return;
            }

            var textChannel = await ResolveTextChannelAsync(guild.RaidAlertChannelId.Value, "raid alert", guildId);
            if (textChannel == null)
            {
                return;
            }

            var embed = await messageGeneration.GenerateRaidAlert(guildId);

            MessageComponent? components = null;
            var webAppBaseUrl = configuration["WebApp:BaseUrl"];
            if (!string.IsNullOrEmpty(webAppBaseUrl))
            {
                components = new ComponentBuilder()
                    .WithButton("View Live Raid", style: ButtonStyle.Link, url: $"{webAppBaseUrl}/live-raid")
                    .Build();
            }

            await textChannel.SendMessageAsync(text: "@everyone", embeds: [embed], components: components);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post raid alert for guild {GuildId}.", guildId);
        }
    }

    public async Task PostRaidEndedAsync(FightsReport closedReport, CancellationToken ct = default)
    {
        var guildId = closedReport.GuildId;
        try
        {
            var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == guildId);
            if (guild == null || guild.LogReportChannelId == null)
            {
                return;
            }

            var (messages, raidWebAppUrl) = await messageGeneration.GenerateRaidReport(closedReport, guildId);
            if (messages == null || messages.Count == 0)
            {
                return;
            }

            var textChannel = await ResolveTextChannelAsync(guild.LogReportChannelId.Value, "log report", guildId);
            if (textChannel == null)
            {
                return;
            }

            var bestTimesIndex = RaidCommandCommandService.BestTimesTargetIndex(messages);
            var bestTimesButtonId = bestTimesIndex.HasValue
                ? $"{ButtonId.BestTimesPvEPrefix}{closedReport.FightsReportId}"
                : null;

            for (var i = 0; i < messages.Count; i++)
            {
                var cb = new ComponentBuilder();
                var hasButton = false;
                if (i == messages.Count - 1 && raidWebAppUrl != null)
                {
                    cb.WithButton("View on DonBot", style: ButtonStyle.Link, url: raidWebAppUrl);
                    hasButton = true;
                }
                if (i == bestTimesIndex && bestTimesButtonId != null)
                {
                    cb.WithButton("Best Times", bestTimesButtonId);
                    hasButton = true;
                }
                await textChannel.SendMessageAsync(embeds: [messages[i]], components: hasButton ? cb.Build() : null);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post raid report for guild {GuildId}.", guildId);
        }
    }

    private async Task<RestTextChannel?> ResolveTextChannelAsync(long channelId, string purpose, long guildId)
    {
        var client = await clientProvider.GetClientAsync();
        var channel = await client.GetChannelAsync((ulong)channelId);
        if (channel is RestTextChannel textChannel)
        {
            return textChannel;
        }
        logger.LogWarning("{Purpose} channel {ChannelId} for guild {GuildId} is not a text channel.", purpose, channelId, guildId);
        return null;
    }
}
