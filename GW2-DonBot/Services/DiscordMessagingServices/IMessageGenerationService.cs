﻿using Discord;
using Models;

namespace Services.DiscordMessagingServices
{
    public interface IMessageGenerationService
    {
        public Embed GenerateFightSummary(EliteInsightDataModel data, ulong guildId);

        public Embed GenerateWvWFightSummary(EliteInsightDataModel data);

        public Embed GeneratePvEFightSummary(EliteInsightDataModel data);

        public Embed GenerateWvWPlayerSummary();
    }
}
