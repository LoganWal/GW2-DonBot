using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IWvWFightSummaryService
{
    Task<Embed> Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client);
    
    Task<Embed> GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players, EmbedBuilder message, long guildId, StatTotals? statTotals = null);
}
