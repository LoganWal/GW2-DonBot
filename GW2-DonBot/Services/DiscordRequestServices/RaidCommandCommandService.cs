using Discord;
using Discord.WebSocket;
using DonBot.Core.Services.RaidLifecycle;
using DonBot.Models.Entities;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;
using Microsoft.Extensions.Configuration;

namespace DonBot.Services.DiscordRequestServices;

public sealed class RaidCommandCommandService(
    IEntityService entityService,
    IMessageGenerationService messageGenerationService,
    IRaidLifecycleService raidLifecycleService,
    IConfiguration configuration) : IRaidCommandService
{
    private MessageComponent? BuildLiveRaidComponents(long guildId)
    {
        var webAppBaseUrl = configuration["WebApp:BaseUrl"];
        if (string.IsNullOrEmpty(webAppBaseUrl))
        {
            return null;
        }
        return new ComponentBuilder()
            .WithButton("View Live Raid", style: ButtonStyle.Link, url: $"{webAppBaseUrl}/live-raid?guild={guildId}")
            .Build();
    }

    public async Task StartRaid(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("Failed to start raid, make sure to use this command within a discord server.", ephemeral: true);
            return;
        }

        var guildId = (long)command.GuildId;
        var result = await raidLifecycleService.OpenRaidAsync(guildId);

        switch (result.Outcome)
        {
            case RaidOpenOutcome.GuildNotConfigured:
                await command.FollowupAsync("This discord server doesn't have raids enabled.", ephemeral: true);
                return;
            case RaidOpenOutcome.AlreadyOpen:
                await command.FollowupAsync("There already exists a raid, close any existing raids.", ephemeral: true);
                return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(s => s.GuildId == guildId);
        if (guild != null && guild.RaidAlertEnabled)
        {
            if (guild.RaidAlertChannelId == null)
            {
                await command.FollowupAsync("There is no raid alert channel set, however the raid has started!", ephemeral: true);
                return;
            }

            if (discordClient.GetChannel((ulong)guild.RaidAlertChannelId) is not ITextChannel targetChannel)
            {
                await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
                return;
            }

            var message = messageGenerationService.GenerateRaidAlert(guild.GuildId);
            await targetChannel.SendMessageAsync(text: "@everyone", embeds: [await message], components: BuildLiveRaidComponents(guild.GuildId));
            await command.FollowupAsync("Created!", ephemeral: true);
        }

        await command.FollowupAsync("Raid has started!", ephemeral: true);
    }

    public async Task CloseRaid(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("Failed to start raid, make sure to use this command within a discord server.", ephemeral: true);
            return;
        }

        var guildId = (long)command.GuildId;
        var result = await raidLifecycleService.CloseRaidAsync(guildId);
        if (result.Outcome == RaidCloseOutcome.NoneOpen || result.Report == null)
        {
            await command.FollowupAsync("No current raid running.", ephemeral: true);
            return;
        }

        var closedReport = result.Report;

        var (messages, raidWebAppUrl) = await messageGenerationService.GenerateRaidReport(closedReport, guildId);
        if (messages == null)
        {
            await command.FollowupAsync("No logs found, closing raid!", ephemeral: true);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == guildId);
        if (guild == null)
        {
            await command.FollowupAsync("Cannot find the related discord, try the command in the discord you want the raffle in!", ephemeral: true);
            return;
        }

        if (guild.LogReportChannelId == null)
        {
            await command.FollowupAsync("No log channel set", ephemeral: true);
            return;
        }

        if (discordClient.GetChannel((ulong)guild.LogReportChannelId) is not ITextChannel targetChannel)
        {
            await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
            return;
        }

        string? bestTimesButtonId = null;
        var bestTimesIndex = BestTimesTargetIndex(messages);
        if (bestTimesIndex.HasValue)
        {
            bestTimesButtonId = $"{ButtonId.BestTimesPvEPrefix}{closedReport.FightsReportId}";
        }

        for (var i = 0; i < messages.Count; i++)
        {
            var cb = new ComponentBuilder();
            var hasButton = false;
            if (i == messages.Count - 1 && raidWebAppUrl != null) { cb.WithButton("View on DonBot", style: ButtonStyle.Link, url: raidWebAppUrl); hasButton = true; }
            if (i == bestTimesIndex && bestTimesButtonId != null) { cb.WithButton("Best Times", bestTimesButtonId); hasButton = true; }
            await targetChannel.SendMessageAsync(embeds: [messages[i]], components: hasButton ? cb.Build() : null);
        }

        await command.FollowupAsync("Created!", ephemeral: true);
    }

    /// <summary>
    /// Returns the index of the message that should receive the Best Times button,
    /// or null if no button should be added (i.e. no PvE embed in the list).
    /// Always targets the last message in the list.
    /// </summary>
    public static int? BestTimesTargetIndex(IReadOnlyList<Embed> messages) =>
        messages.Count > 0 && messages.Any(m => m.Title?.Contains("PvE") == true)
            ? messages.Count - 1
            : null;

    public async Task StartAllianceRaid(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("Failed to start raid, make sure to use this command within a discord server.", ephemeral: true);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(s => s.GuildId == (long)command.GuildId);
        if (guild == null)
        {
            await command.FollowupAsync("This discord server doesn't have raids enabled.", ephemeral: true);
            return;
        }

        if (guild.RaidAlertEnabled)
        {
            if (guild.RaidAlertChannelId == null)
            {
                await command.FollowupAsync("There is no raid alert channel set, however the raid has started!", ephemeral: true);
                return;
            }

            if (discordClient.GetChannel((ulong)guild.RaidAlertChannelId) is not ITextChannel targetChannel)
            {
                await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
                return;
            }

            var raidMessage = command.Data.Options.FirstOrDefault(x => x.Name == "raid-message")?.Value?.ToString();
            var message = messageGenerationService.GenerateRaidAlert(guild.GuildId);

            await targetChannel.SendMessageAsync(text: $"@everyone {raidMessage}", embeds: [await message], components: BuildLiveRaidComponents(guild.GuildId));
            await command.FollowupAsync("Created!", ephemeral: true);
        }

        await command.FollowupAsync("Raid has started!", ephemeral: true);
    }
}
