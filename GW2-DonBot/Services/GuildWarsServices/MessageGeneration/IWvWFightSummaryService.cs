using Discord;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed record WvWFightSummaryRenderResult(
    Embed Embed,
    string? WebAppUrl,
    long? FightLogId,
    string StreamMessage);

public interface IWvWFightSummaryService
{
    Task<(Embed Embed, string? WebAppUrl, long? FightLogId)> Generate(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client);

    Task<WvWFightSummaryRenderResult> Render(
        EliteInsightDataModel data,
        bool advancedLog,
        Guild guild,
        FightLog? fightLog);

    Task<Embed> GenerateMessage(bool advancedLog, int playerCount, List<Gw2Player> gw2Players, EmbedBuilder message, long guildId, StatTotals? statTotals = null);
}
