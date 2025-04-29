using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices
{
    public interface IMessageGenerationService
    {
        public Task<Embed> GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client);

        public Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration);

        public Task<Embed> GeneratePvEFightSummary(EliteInsightDataModel data, long guildId);

        public Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild);

        public Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl);

        public Task<List<Embed>?> GenerateRaidReport(FightsReport fightsReport, long guildId);

        public Task<List<Embed>?> GenerateRaidReplyReport(List<string> urls);

        public Task<Embed> GenerateRaidAlert(long guildId);
    }
}
