using Discord;
using Discord.WebSocket;
using Models.Entities;
using Models.Enums;
using Services.LogGenerationServices;

namespace Services.DiscordRequestServices
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

        public async Task StartRaid(SocketSlashCommand command)
        {
            // Defer the command execution
            await command.DeferAsync(ephemeral: true);
            if (command.GuildId == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to start raid, make sure to use this command within a discord server.");
                return;
            }

            var existingOpenRaids = _databaseContext.FightsReport.FirstOrDefault(s => s.GuildId == (long)command.GuildId && s.FightsEnd == null);
            if (existingOpenRaids != null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There already exists a raid, close any existing raids.");
                return;
            }

            var raid = new FightsReport
            {
                GuildId = (long)command.GuildId,
                FightsStart = DateTime.UtcNow
            };

            _databaseContext.Add(raid);
            _databaseContext.SaveChanges();

            await command.ModifyOriginalResponseAsync(m => m.Content = "Raid has started!");
        }

        public async Task CloseRaid(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the command execution
            await command.DeferAsync(ephemeral: true);
            if (command.GuildId == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to start raid, make sure to use this command within a discord server.");
                return;
            }

            var existingOpenRaid = _databaseContext.FightsReport.FirstOrDefault(s => s.GuildId == (long)command.GuildId && s.FightsEnd == null);
            if (existingOpenRaid == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "No current raid running.");
                return;
            }

            existingOpenRaid.FightsEnd = DateTime.UtcNow;

            _databaseContext.Update(existingOpenRaid);
            _databaseContext.SaveChanges();

            var message = _messageGenerationService.GenerateRaidReport(existingOpenRaid);
            if (message == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Error creating report!");
                return;
            }

            var guild = _databaseContext.Guild.FirstOrDefault(g => g.GuildId == (long)command.GuildId);
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Cannot find the related discord, try the command in the discord you want the raffle in!");
                return;
            }

            if (guild.LogReportChannelId == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "No log channel set");
                return;
            }

            if (discordClient.GetChannel((ulong)guild.LogReportChannelId) is not ITextChannel targetChannel)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to find the target channel.");
                return;
            }

            // Send to target channel with components
            await targetChannel.SendMessageAsync(embeds: new[] { message });

            await command.ModifyOriginalResponseAsync(m => m.Content = "Created!");
        }
    }
}
