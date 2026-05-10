using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using DonBot.Models.Apis.GuildWars2Api;

namespace DonBot.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").RequireAuthorization();
        group.MapPost("/verify", VerifyGw2Key);
        group.MapDelete("/gw2/{accountId}", RemoveGw2Account);
        group.MapGet("/gw2", GetGw2Accounts);
    }

    private record VerifyRequest(string ApiKey);

    private static async Task<IResult> VerifyGw2Key(
        VerifyRequest request,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IHttpClientFactory httpClientFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId)) {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey)) {
            return Results.BadRequest("API key is required.");
        }

        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={request.ApiKey}");

        if (!response.IsSuccessStatusCode) {
            return Results.BadRequest("Invalid GW2 API key.");
        }

        var json = await response.Content.ReadAsStringAsync();
        var accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(json) ?? new GuildWars2AccountDataModel();

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId);
        if (account == null)
        {
            account = new Account { DiscordId = discordId };
            context.Account.Add(account);
            await context.SaveChangesAsync();
        }

        var existing = await context.GuildWarsAccount
            .AsTracking()
            .FirstOrDefaultAsync(g => g.DiscordId == discordId && g.GuildWarsAccountId == accountData.Id);

        if (existing != null)
        {
            existing.GuildWarsAccountName = accountData.Name;
            existing.GuildWarsApiKey = request.ApiKey;
            existing.GuildWarsGuilds = string.Join(',', accountData.Guilds);
            existing.World = Convert.ToInt32(accountData.World);
        }
        else
        {
            context.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = accountData.Id,
                DiscordId = discordId,
                GuildWarsApiKey = request.ApiKey,
                GuildWarsAccountName = accountData.Name,
                GuildWarsGuilds = string.Join(',', accountData.Guilds),
                World = Convert.ToInt32(accountData.World)
            });
        }

        await context.SaveChangesAsync();

        return Results.Ok(new { accountName = accountData.Name, isNew = existing == null });
    }

    private static async Task<IResult> RemoveGw2Account(
        string accountId,
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId)) {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(accountId, out var accountGuid)) {
            return Results.BadRequest("Invalid account ID.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var account = await context.GuildWarsAccount
            .FirstOrDefaultAsync(g => g.DiscordId == discordId && g.GuildWarsAccountId == accountGuid);

        if (account == null) {
            return Results.NotFound();
        }

        context.GuildWarsAccount.Remove(account);
        await context.SaveChangesAsync();

        return Results.Ok();
    }

    private static async Task<IResult> GetGw2Accounts(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        if (!long.TryParse(discordIdStr, out var discordId)) {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var accounts = await context.GuildWarsAccount
            .Where(g => g.DiscordId == discordId)
            .Select(g => new { g.GuildWarsAccountId, g.GuildWarsAccountName })
            .ToListAsync();

        return Results.Ok(accounts);
    }
}
