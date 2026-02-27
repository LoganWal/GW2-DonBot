using Discord;
using DonBot.Models.Entities;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IWvWPlayerSummaryService
{
    Task<Embed> Generate(Guild gw2Guild);
    
    Task<Embed> GenerateActive(Guild gw2Guild, string fightLogUrl);
}
