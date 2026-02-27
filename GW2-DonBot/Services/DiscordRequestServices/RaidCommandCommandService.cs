using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class RaidCommandCommandService(IEntityService entityService, IMessageGenerationService messageGenerationService) : IRaidCommandService
{
    public async Task StartRaid(SocketSlashCommand command, DiscordSocketClient discordClient)
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

        var existingOpenRaids = await entityService.FightsReport.GetFirstOrDefaultAsync(s => s.GuildId == (long)command.GuildId && s.FightsEnd == null);
        if (existingOpenRaids != null)
        {
            await command.FollowupAsync("There already exists a raid, close any existing raids.", ephemeral: true);
            return;
        }

        var raid = new FightsReport
        {
            GuildId = (long)command.GuildId,
            FightsStart = DateTime.UtcNow
        };

        await entityService.FightsReport.AddAsync(raid);

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

            var message = messageGenerationService.GenerateRaidAlert(guild.GuildId);
            await targetChannel.SendMessageAsync(text: "@everyone", embeds: [await message]);
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

        var existingOpenRaid = await entityService.FightsReport.GetFirstOrDefaultAsync(s => s.GuildId == (long)command.GuildId && s.FightsEnd == null);
        if (existingOpenRaid == null)
        {
            await command.FollowupAsync("No current raid running.", ephemeral: true);
            return;
        }

        existingOpenRaid.FightsEnd = DateTime.UtcNow;

        var messages = await messageGenerationService.GenerateRaidReport(existingOpenRaid, (long)command.GuildId);
        if (messages == null)
        {
            await command.FollowupAsync("No logs found, closing raid!", ephemeral: true);
            await entityService.FightsReport.UpdateAsync(existingOpenRaid);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);
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

        // Send to target channel with components
        var firstPveMessage = messages.FirstOrDefault(m => m.Title?.Contains("PvE") == true);
        MessageComponent? bestTimesComponent = null;
        if (firstPveMessage != null)
        {
            bestTimesComponent = new ComponentBuilder()
                .WithButton("Best Times", $"{ButtonId.BestTimesPvEPrefix}{existingOpenRaid.FightsReportId}")
                .Build();
        }

        foreach (var message in messages)
        {
            var components = message == firstPveMessage ? bestTimesComponent : null;
            await targetChannel.SendMessageAsync(embeds: [message], components: components);
        }

        await entityService.FightsReport.UpdateAsync(existingOpenRaid);

        await command.FollowupAsync("Created!", ephemeral: true);
    }

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

            await targetChannel.SendMessageAsync(text: $"@everyone {raidMessage}", embeds: [await message]);
            await command.FollowupAsync("Created!", ephemeral: true);
        }

        await command.FollowupAsync("Raid has started!", ephemeral: true);
    }
}