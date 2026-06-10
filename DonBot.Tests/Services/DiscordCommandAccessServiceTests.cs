using DonBot.Api.Services;

namespace DonBot.Tests.Services;

public class DiscordCommandAccessServiceTests
{
    [Fact]
    public void PermissionsAllow_WithMissingPermissionPayload_AllowsCommand()
    {
        var allowed = DiscordCommandAccessService.PermissionsAllow(
            null,
            guildId: 42,
            userId: 123,
            roleIds: []);

        Assert.True(allowed);
    }
}
