using Discord;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IFooterService
{
    Task<string> Generate(long guildId);
    void AddWidthSpacer(EmbedBuilder builder);
    void AddInviteLink(EmbedBuilder builder);
}
