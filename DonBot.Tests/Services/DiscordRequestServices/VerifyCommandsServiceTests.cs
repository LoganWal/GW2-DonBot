using DonBot.Core.Models.Entities;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Services.DiscordRequestServices;
using DonBot.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.DiscordRequestServices;

public class VerifyCommandsServiceTests
{
    [Fact]
    public async Task UpsertGuildWarsAccountAsync_ExistingGw2AccountForOtherDiscordId_TransfersLink()
    {
        var accountId = Guid.NewGuid();
        var entityService = new InMemoryEntityService();
        await entityService.Account.AddAsync(new Account { DiscordId = 222L });
        await entityService.GuildWarsAccount.AddAsync(new GuildWarsAccount
        {
            GuildWarsAccountId = accountId,
            DiscordId = 111L,
            GuildWarsAccountName = "Jezelle.4107",
            GuildWarsApiKey = null,
            GuildWarsGuilds = "old-guild",
            World = 1000
        });

        var service = new VerifyCommandsService(
            entityService,
            NullLogger<VerifyCommandsService>.Instance,
            new StubHttpClientFactory(new StubHttpMessageHandler()));

        await service.UpsertGuildWarsAccountAsync(222L, new GuildWars2AccountDataModel
        {
            Id = accountId,
            Name = "Jezelle.4107",
            Guilds = ["new-guild", "ally-guild"],
            World = 2202
        }, "new-key");

        var stored = await entityService.GuildWarsAccount.GetFirstOrDefaultAsync(g => g.GuildWarsAccountId == accountId);
        Assert.NotNull(stored);
        Assert.Equal(222L, stored.DiscordId);
        Assert.Equal("new-key", stored.GuildWarsApiKey);
        Assert.Equal("new-guild,ally-guild", stored.GuildWarsGuilds);
        Assert.Equal(2202, stored.World);
    }
}
