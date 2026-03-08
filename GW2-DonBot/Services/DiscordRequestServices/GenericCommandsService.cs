using System.Text;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class GenericCommandsService(IEntityService entityService) : IGenericCommandsService
{
    public async Task HelpCommandExecuted(SocketSlashCommand command)
    {
        var message = new StringBuilder();

        message.AppendLine("**/help**")
            .AppendLine("*The output of this command will only be visible to you.*")
            .AppendLine("This is where you are now! Use this to get help on how some commands work.")
            .AppendLine()
            .AppendLine("**/verify**")
            .AppendLine("*The output of this command will only be visible to you.*")
            .AppendLine("This command can be used to link your GW2 and Discord accounts via a GW2 API key! This is required to have access to some roles, and will give you access to future features once they're developed! Once verified, you won't need to use this command again unless you wish to update your details.")
            .AppendLine("`[api-key]` This is your GW2 API key, make sure it has guild and account permissions!")
            .AppendLine()
            .AppendLine("**/deverify**")
            .AppendLine("*The output of this command will only be visible to you.*")
            .AppendLine("This command can be used to remove any currently stored data associated with your Discord account. The data stored via the /verify command can be wiped through this. Note you will have to re-verify to access certain roles and features! This will only remove the information associated with the Discord account used to trigger the command.");

        await command.FollowupAsync(message.ToString(), ephemeral: true);
    }

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