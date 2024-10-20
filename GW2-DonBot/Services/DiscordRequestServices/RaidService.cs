﻿using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Services.LogGenerationServices;

namespace DonBot.Services.DiscordRequestServices
{
    public class RaidService : IRaidService
    {
        private readonly IMessageGenerationService _messageGenerationService;
        private readonly DatabaseContext _databaseContext;

        public RaidService(IMessageGenerationService messageGenerationService, DatabaseContext databaseContext)
        {
            _messageGenerationService = messageGenerationService;
            _databaseContext = databaseContext;
        }

        public async Task StartRaid(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            if (command.GuildId == null)
            {
                await command.FollowupAsync("Failed to start raid, make sure to use this command within a discord server.", ephemeral: true);
                return;
            }

            var guild = _databaseContext.Guild.FirstOrDefault(s => s.GuildId == (long)command.GuildId);
            if (guild == null)
            {
                await command.FollowupAsync("This discord server doesn't have raids enabled.", ephemeral: true);
                return;
            }

            var existingOpenRaids = _databaseContext.FightsReport.FirstOrDefault(s => s.GuildId == (long)command.GuildId && s.FightsEnd == null);
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

            _databaseContext.Add(raid);
            await _databaseContext.SaveChangesAsync();

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

                var message = _messageGenerationService.GenerateRaidAlert(guild.GuildId);
                await targetChannel.SendMessageAsync(text: "@everyone", embeds: new[] { message });
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

            var existingOpenRaid = _databaseContext.FightsReport.FirstOrDefault(s => s.GuildId == (long)command.GuildId && s.FightsEnd == null);
            if (existingOpenRaid == null)
            {
                await command.FollowupAsync("No current raid running.", ephemeral: true);
                return;
            }

            existingOpenRaid.FightsEnd = DateTime.UtcNow;

            var messages = _messageGenerationService.GenerateRaidReport(existingOpenRaid, (long)command.GuildId);
            if (messages == null)
            {
                await command.FollowupAsync("No logs found, closing raid!", ephemeral: true);
                _databaseContext.Update(existingOpenRaid);
                await _databaseContext.SaveChangesAsync();
                return;
            }

            var guild = _databaseContext.Guild.FirstOrDefault(g => g.GuildId == (long)command.GuildId);
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
            foreach (var message in messages)
            {
                await targetChannel.SendMessageAsync(embeds: new[] { message });
            }

            _databaseContext.Update(existingOpenRaid);
            await _databaseContext.SaveChangesAsync();

            await command.FollowupAsync("Created!", ephemeral: true);
        }
    }
}
