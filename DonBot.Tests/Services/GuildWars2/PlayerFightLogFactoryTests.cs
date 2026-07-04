using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services;
using DonBot.Core.Services.GuildWars2;

namespace DonBot.Tests.Services.GuildWars2;

public class PlayerFightLogFactoryTests
{
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
        Assert.Equal(72.56m, result.QuicknessGenGroup);
        Assert.Equal(0m, result.StabGenOnGroup);
        Assert.Equal(7.89m, result.StabGenOffGroup);
        Assert.Equal(123.46m, result.DistanceFromTag);
        Assert.Equal(PlayerFightLogRoleClassifier.BoonDpsRole, result.BoonRole);
        Assert.Equal(PlayerFightLogPlaystyleClassifier.BoonDpsPlaystyle, result.Playstyle);
    }
}
