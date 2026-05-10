using System.Net;
using System.Net.Http.Json;
using System.Text;
using DonBot.Api.Endpoints;
using DonBot.Models.Entities;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Tests.Services.ApiEndpoints;

public class AccountEndpointsIntegrationTests
{
    private static MinimalApiHost NewHost(HttpMessageHandler? gw2Handler = null) =>
        new(app => app.MapAccountEndpoints(), httpHandler: gw2Handler);

    [Fact]
    public async Task GetGw2Accounts_NoAuth_Returns401()
    {
        using var host = NewHost();

        var response = await host.Client.GetAsync("/api/account/gw2");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGw2Accounts_AuthedNoAccounts_ReturnsEmptyArray()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.GetAsync("/api/account/gw2");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task GetGw2Accounts_AuthedWithAccounts_ReturnsOnlyOwn()
    {
        using var host = NewHost();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.AddRange(
                new GuildWarsAccount { GuildWarsAccountId = Guid.NewGuid(), DiscordId = 123L, GuildWarsAccountName = "Mine.1234" },
                new GuildWarsAccount { GuildWarsAccountId = Guid.NewGuid(), DiscordId = 456L, GuildWarsAccountName = "Other.5678" });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var body = await host.Client.GetStringAsync("/api/account/gw2");

        Assert.Contains("Mine.1234", body);
        Assert.DoesNotContain("Other.5678", body);
    }

    [Fact]
    public async Task RemoveGw2Account_InvalidGuid_Returns400()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.DeleteAsync("/api/account/gw2/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveGw2Account_NotFound_Returns404()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.DeleteAsync($"/api/account/gw2/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveGw2Account_ForOtherUser_Returns404()
    {
        using var host = NewHost();
        var accountId = Guid.NewGuid();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = accountId,
                DiscordId = 999L,
                GuildWarsAccountName = "NotMine.1234"
            });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.DeleteAsync($"/api/account/gw2/{accountId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveGw2Account_ForSelf_DeletesAndReturns200()
    {
        using var host = NewHost();
        var accountId = Guid.NewGuid();
        await using (var db = await host.DbFactory.CreateDbContextAsync())
        {
            db.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = accountId,
                DiscordId = 123L,
                GuildWarsAccountName = "Mine.1234"
            });
            await db.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.DeleteAsync($"/api/account/gw2/{accountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verify = await host.DbFactory.CreateDbContextAsync();
        Assert.Empty(verify.GuildWarsAccount);
    }

    [Fact]
    public async Task VerifyGw2Key_EmptyKey_Returns400()
    {
        using var host = NewHost();
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/account/verify", new { ApiKey = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyGw2Key_InvalidApiKey_GW2Returns401_BadRequestPropagated()
    {
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var host = NewHost(handler);
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/account/verify", new { ApiKey = "bad-key" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyGw2Key_NewLink_AddsAccountAndDonBotAccount()
    {
        var accountGuid = Guid.NewGuid();
        var json = $$"""
                     {"id":"{{accountGuid}}","name":"NewPlayer.1234","world":2202,"guilds":["g1","g2"]}
                     """;
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var host = NewHost(handler);
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/account/verify", new { ApiKey = "valid-key" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("NewPlayer.1234", body);
        Assert.Contains("\"isNew\":true", body);

        await using var db = await host.DbFactory.CreateDbContextAsync();
        Assert.Single(db.Account);
        var stored = db.GuildWarsAccount.Single();
        Assert.Equal("g1,g2", stored.GuildWarsGuilds);
        Assert.Equal(2202, stored.World);
    }

    [Fact]
    public async Task VerifyGw2Key_ReverifyExistingAccount_UpdatesInPlace()
    {
        var accountGuid = Guid.NewGuid();
        var json = $$"""
                     {"id":"{{accountGuid}}","name":"Same.1234","world":3000,"guilds":["new-guild"]}
                     """;
        var handler = new ApiStubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        using var host = NewHost(handler);
        await using (var seed = await host.DbFactory.CreateDbContextAsync())
        {
            seed.Account.Add(new Account { DiscordId = 123L });
            seed.GuildWarsAccount.Add(new GuildWarsAccount
            {
                GuildWarsAccountId = accountGuid,
                DiscordId = 123L,
                GuildWarsAccountName = "Same.1234",
                GuildWarsGuilds = "old-guild",
                World = 1000,
                GuildWarsApiKey = "old-key"
            });
            await seed.SaveChangesAsync();
        }
        host.AuthenticateAs(123L);

        var response = await host.Client.PostAsJsonAsync("/api/account/verify", new { ApiKey = "new-key" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"isNew\":false", body);

        // DatabaseContext defaults to NoTracking, so the endpoint must use AsTracking() on the
        // re-verify lookup for the in-place mutations to be persisted. This test guards against
        // a regression where AsTracking is dropped.
        await using var verify = await host.DbFactory.CreateDbContextAsync();
        var stored = verify.GuildWarsAccount.Single();
        Assert.Equal("new-key", stored.GuildWarsApiKey);
        Assert.Equal("new-guild", stored.GuildWarsGuilds);
        Assert.Equal(3000, stored.World);
    }

    private sealed class ApiStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
