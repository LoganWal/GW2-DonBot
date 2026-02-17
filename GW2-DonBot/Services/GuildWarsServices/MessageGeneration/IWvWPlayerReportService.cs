using Discord;
using DonBot.Models.Entities;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IWvWPlayerReportService
{
    Task<Embed> Generate(Guild guildConfiguration);
}
