using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;
using DonBot.Core.Services.GuildWars2;

namespace DonBot.Tests.Services.GuildWars2;

public class PlayerFightLogFactoryTests
{
    [Theory]
    [InlineData(87.126, 87.13)]
    [InlineData(double.NaN, 0)]
    public void CreateWvw_PreservesRoundedRegenAndGuardsNonFiniteValues(double regen, double expected)
    {
        var player = new Gw2Player { AccountName = "Player.1234", TotalRegen = regen };

        var result = Assert.Single(PlayerFightLogFactory.CreateWvw([player], 42, 60_000));

        Assert.Equal((decimal)expected, result.RegenDuration);
    }

    [Fact]
    public void CreatePve_RoundsFiniteDecimalsAndGuardsNonFiniteValues()
    {
        var player = new Gw2Player
        {
            AccountName = "Player.1234",
            CharacterName = "Character",
            Damage = 600_000,
            TotalQuick = 12.346,
            TotalAlac = double.PositiveInfinity,
            TotalRegen = 87.126,
            QuicknessGenGroup = 72.556,
            AlacGenGroup = 0,
            StabOnGroup = double.NaN,
            StabOffGroup = 7.894,
            DistanceFromTag = 123.456
        };

        var result = Assert.Single(PlayerFightLogFactory.CreatePve([player], fightLogId: 42, fightDurationInMs: 60_000));

        Assert.Equal(42, result.FightLogId);
        Assert.Equal("Player.1234", result.GuildWarsAccountName);
        Assert.Equal(12.35m, result.QuicknessDuration);
        Assert.Equal(0m, result.AlacDuration);
        Assert.Equal(87.13m, result.RegenDuration);
        Assert.Equal(72.56m, result.QuicknessGenGroup);
        Assert.Equal(0m, result.StabGenOnGroup);
        Assert.Equal(7.89m, result.StabGenOffGroup);
        Assert.Equal(123.46m, result.DistanceFromTag);
        Assert.Equal(PlayerFightLogRoleClassifier.BoonDpsRole, result.BoonRole);
        Assert.Equal(PlayerFightLogPlaystyleClassifier.BoonDpsPlaystyle, result.Playstyle);
    }
}
