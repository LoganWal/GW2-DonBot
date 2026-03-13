using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class GenericCommandsService(IEntityService entityService) : IGenericCommandsService
{
    public async Task AddQuoteCommandExecuted(SocketSlashCommand command)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("This command must be used within a Discord server.", ephemeral: true);
            return;
        }

        var quote = (string)command.Data.Options.First().Value;
        if (string.IsNullOrWhiteSpace(quote))
        {
            await command.FollowupAsync("Quote cannot be empty.", ephemeral: true);
            return;
        }

        await entityService.GuildQuote.AddAsync(new GuildQuote
        {
            GuildId = (long)command.GuildId,
            Quote = quote.Trim()
        });

        await command.FollowupAsync("Quote added!", ephemeral: true);
    }
}