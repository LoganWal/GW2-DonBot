using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Core.Services.GuildWars2;

public static class FightLogRawDataStore
{
    public static async Task UpsertAsync(
        DatabaseContext ctx,
        long fightLogId,
        EliteInsightDataModel data,
        CancellationToken ct = default)
    {
        var existing = await ctx.FightLogRawData.FirstOrDefaultAsync(r => r.FightLogId == fightLogId, ct);
        if (existing != null)
        {
            existing.RawFightData = data.RawFightData;
            existing.RawHealingData = data.RawHealingData;
            existing.RawBarrierData = data.RawBarrierData;
            ctx.FightLogRawData.Update(existing);
        }
        else
        {
            ctx.FightLogRawData.Add(new FightLogRawData
            {
                FightLogId = fightLogId,
                RawFightData = data.RawFightData,
                RawHealingData = data.RawHealingData,
                RawBarrierData = data.RawBarrierData
            });
        }

        await ctx.SaveChangesAsync(ct);
    }
}
