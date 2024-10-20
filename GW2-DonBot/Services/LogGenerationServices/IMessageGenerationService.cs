﻿using Discord;
using Discord.WebSocket;
using DonBot.Models;
using DonBot.Models.Entities;

namespace DonBot.Services.LogGenerationServices
{
    public interface IMessageGenerationService
    {
        public Embed GenerateWvWFightSummary(EliteInsightDataModel data, bool advancedLog, Guild guild, DiscordSocketClient client);

        public Task<Embed> GenerateWvWPlayerReport(Guild guildConfiguration);

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data, long guildId);

        public Task<Embed> GenerateWvWPlayerSummary(Guild gw2Guild);

        public Task<Embed> GenerateWvWActivePlayerSummary(Guild gw2Guild, string fightLogUrl);

        public List<Embed>? GenerateRaidReport(FightsReport fightsReportId, long guildId);

        public Embed GenerateRaidAlert(long guildId);
    }
}
