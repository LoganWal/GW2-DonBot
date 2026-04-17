using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices;

public interface IMessageGenerationService
{
    public Task<(Embed Embed, string? WebAppUrl)> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client);

    public Task<(Embed Embed, string? WebAppUrl)> GeneratePvEFightSummary(EliteInsightDataModel data, long guildId);

    public Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateRaidReport(FightsReport fightsReport, long guildId);

    public Task<(List<Embed>? Embeds, string? WebAppUrl)> GenerateRaidReplyReport(List<string> urls, long guildId);

    public Task<Embed> GenerateRaidAlert(long guildId);
}