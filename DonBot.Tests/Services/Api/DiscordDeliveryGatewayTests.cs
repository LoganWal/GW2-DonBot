using Discord;
using DonBot.Api.Services;

namespace DonBot.Tests.Services.Api;

public class DiscordDeliveryGatewayTests
{
    [Fact]
    public void HasRequiredPermissions_RequiresViewSendAndEmbed()
    {
        Assert.True(DiscordDeliveryGateway.HasRequiredPermissions(ChannelPermissions.Text));
        Assert.False(DiscordDeliveryGateway.HasRequiredPermissions(
            ChannelPermissions.Text.Modify(viewChannel: false)));
        Assert.False(DiscordDeliveryGateway.HasRequiredPermissions(
            ChannelPermissions.Text.Modify(sendMessages: false)));
        Assert.False(DiscordDeliveryGateway.HasRequiredPermissions(
            ChannelPermissions.Text.Modify(embedLinks: false)));
        Assert.False(DiscordDeliveryGateway.HasRequiredPermissions(ChannelPermissions.None));
    }
}
