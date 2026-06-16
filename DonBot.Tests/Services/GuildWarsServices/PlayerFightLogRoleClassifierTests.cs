using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;

namespace DonBot.Tests.Services.GuildWarsServices;

public class PlayerFightLogRoleClassifierTests
{
    [Fact]
    public void ResolveBoonRole_WhenQuicknessGenerationIsHighAndDpsIsHealthy_ReturnsBoonDps()
    {
        var player = new Gw2Player
        {
            Damage = 300_000,
            QuicknessGenGroup = 75
        };

        var role = PlayerFightLogRoleClassifier.ResolveBoonRole(player, 60_000, averageGroupDps: 10_000);

        Assert.Equal(PlayerFightLogRoleClassifier.BoonDpsRole, role);
    }

    [Fact]
    public void ResolveBoonRole_WhenAlacrityGenerationIsHighAndDpsIsLow_ReturnsBoonHealer()
    {
        var player = new Gw2Player
        {
            Damage = 120_000,
            AlacGenGroup = 80
        };

        var role = PlayerFightLogRoleClassifier.ResolveBoonRole(player, 60_000, averageGroupDps: 10_000);

        Assert.Equal(PlayerFightLogRoleClassifier.BoonHealerRole, role);
    }

    [Fact]
    public void ResolveBoonRole_WhenGenerationIsMissingAndDpsIsLow_ReturnsEmpty()
    {
        var player = new Gw2Player
        {
            Damage = 120_000
        };

        var role = PlayerFightLogRoleClassifier.ResolveBoonRole(player, 60_000, averageGroupDps: 10_000);

        Assert.Equal(string.Empty, role);
    }

    [Fact]
    public void ResolveBoonRole_WhenGenerationIsNotAboveThirtyFive_ReturnsEmpty()
    {
        var player = new Gw2Player
        {
            Damage = 300_000,
            QuicknessGenGroup = 35,
            AlacGenGroup = 0
        };

        var role = PlayerFightLogRoleClassifier.ResolveBoonRole(player, 60_000, averageGroupDps: 10_000);

        Assert.Equal(string.Empty, role);
    }
}
