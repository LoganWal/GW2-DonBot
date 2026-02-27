namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IFooterService
{
    Task<string> Generate(long guildId);
}
