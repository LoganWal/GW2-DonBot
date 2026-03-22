using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

internal static class FightLogHelpers
{
    internal static string GetLogSource(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "unknown";

    internal static async Task UpsertRawDataAsync(IEntityService entityService, long fightLogId, EliteInsightDataModel data)
    {
        var existing = await entityService.FightLogRawData.GetFirstOrDefaultAsync(r => r.FightLogId == fightLogId);
        if (existing != null)
        {
            existing.RawFightData = data.RawFightData;
            existing.RawHealingData = data.RawHealingData;
            existing.RawBarrierData = data.RawBarrierData;
            await entityService.FightLogRawData.UpdateAsync(existing);
        }
        else
        {
            await entityService.FightLogRawData.AddAsync(new FightLogRawData
            {
                FightLogId = fightLogId,
                RawFightData = data.RawFightData,
                RawHealingData = data.RawHealingData,
                RawBarrierData = data.RawBarrierData
            });
        }
    }
}
