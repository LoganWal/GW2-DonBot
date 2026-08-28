using System.Security.Cryptography;
using System.Text;
using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Core.Services.GuildWars2;

public sealed class DiscordReportDeliveryClaimService(
    IDbContextFactory<DatabaseContext> dbContextFactory)
{
    public const string ApiSource = "api";
    public const string DiscordSource = "discord";

    public async Task<bool> TryClaimAsync(
        long guildId,
        string reportUrl,
        string source,
        CancellationToken ct = default)
    {
        var reportUrlHash = HashReportUrl(reportUrl);
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        context.DiscordReportDeliveryClaim.Add(new DiscordReportDeliveryClaim
        {
            GuildId = guildId, ReportUrlHash = reportUrlHash, Source = source
        });

        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            var exists = await context.DiscordReportDeliveryClaim.AsNoTracking()
                .AnyAsync(
                    claim => claim.GuildId == guildId && claim.ReportUrlHash == reportUrlHash,
                    ct);
            if (exists)
            {
                return false;
            }

            throw;
        }
    }

    internal static string HashReportUrl(string reportUrl)
    {
        var canonicalUrl = ReportUrlHelper.TryParseReportUrl(reportUrl, out var parsed, requireHttps: true)
            ? parsed.CanonicalUrl
            : reportUrl;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl)));
    }
}
