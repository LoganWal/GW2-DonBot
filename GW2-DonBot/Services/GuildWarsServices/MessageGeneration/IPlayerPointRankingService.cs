using Discord;
using DonBot.Core.Models.Entities;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public interface IPlayerPointRankingService
{
    Task<IReadOnlyList<Embed>> Generate(
        Guild guild,
        long fightLogId,
        IReadOnlySet<long> guildMemberDiscordIds);
}
