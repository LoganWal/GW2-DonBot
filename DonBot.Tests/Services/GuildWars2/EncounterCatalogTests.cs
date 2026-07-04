using DonBot.Core.Models.Enums;
using DonBot.Core.Services.GuildWars2;

namespace DonBot.Tests.Services.GuildWars2;

public class EncounterCatalogTests
{
    [Theory]
    [InlineData(131329, FightTypesEnum.Vale, false)]
    [InlineData(131332, FightTypesEnum.Spirit, true)]
    [InlineData(131330, FightTypesEnum.Gorseval, false)]
    [InlineData(131331, FightTypesEnum.Sabetha, false)]
    [InlineData(131585, FightTypesEnum.Sloth, false)]
    [InlineData(131586, FightTypesEnum.Trio, false)]
    [InlineData(131587, FightTypesEnum.Matthias, false)]
    [InlineData(131841, FightTypesEnum.Escort, false)]
    [InlineData(131842, FightTypesEnum.Kc, false)]
    [InlineData(131843, FightTypesEnum.Tc, false)]
    [InlineData(131844, FightTypesEnum.Xera, false)]
    [InlineData(132097, FightTypesEnum.Cairn, false)]
    [InlineData(132098, FightTypesEnum.Mo, false)]
    [InlineData(132099, FightTypesEnum.Samarog, false)]
    [InlineData(132100, FightTypesEnum.Deimos, false)]
    [InlineData(132353, FightTypesEnum.Sh, false)]
    [InlineData(132354, FightTypesEnum.River, false)]
    [InlineData(132355, FightTypesEnum.Bk, false)]
    [InlineData(132356, FightTypesEnum.EoS, false)]
    [InlineData(132357, FightTypesEnum.SoD, false)]
    [InlineData(132358, FightTypesEnum.Dhuum, false)]
    [InlineData(132609, FightTypesEnum.Ca, false)]
    [InlineData(132610, FightTypesEnum.Largos, true)]
    [InlineData(132611, FightTypesEnum.Qadim, false)]
    [InlineData(132865, FightTypesEnum.Adina, false)]
    [InlineData(132866, FightTypesEnum.Sabir, false)]
    [InlineData(132867, FightTypesEnum.Peerless, false)]
    [InlineData(133121, FightTypesEnum.Greer, false)]
    [InlineData(133122, FightTypesEnum.Decima, false)]
    [InlineData(133123, FightTypesEnum.Ura, false)]
    [InlineData(262657, FightTypesEnum.Icebrood, true)]
    [InlineData(262658, FightTypesEnum.Fraenir, true)]
    [InlineData(262659, FightTypesEnum.Kodan, true)]
    [InlineData(262661, FightTypesEnum.Whisper, true)]
    [InlineData(262660, FightTypesEnum.Boneskinner, true)]
    [InlineData(262913, FightTypesEnum.Ah, true)]
    [InlineData(262914, FightTypesEnum.Xjj, true)]
    [InlineData(262915, FightTypesEnum.Ko, true)]
    [InlineData(262916, FightTypesEnum.Ht, true)]
    [InlineData(262917, FightTypesEnum.Olc, true)]
    [InlineData(263425, FightTypesEnum.Co, true)]
    [InlineData(263426, FightTypesEnum.ToF, true)]
    [InlineData(196865, FightTypesEnum.Mama, true)]
    [InlineData(196866, FightTypesEnum.Siax, true)]
    [InlineData(196867, FightTypesEnum.Ensolyss, true)]
    [InlineData(197121, FightTypesEnum.Skorvald, true)]
    [InlineData(197122, FightTypesEnum.Artsariiv, true)]
    [InlineData(197123, FightTypesEnum.Arkk, true)]
    [InlineData(197378, FightTypesEnum.AiEle, true)]
    [InlineData(197379, FightTypesEnum.AiDark, true)]
    [InlineData(197377, FightTypesEnum.AiBoth, true)]
    [InlineData(197633, FightTypesEnum.Kanaxai, true)]
    [InlineData(197890, FightTypesEnum.Eparch, true)]
    [InlineData(198145, FightTypesEnum.Shadow, true)]
    [InlineData(263681, FightTypesEnum.Kela, true)]
    public void ResolvePveEncounter_WhenKnownEncounter_ReturnsFightTypeAndTargetMode(
        long encounterId,
        FightTypesEnum expectedFightType,
        bool expectedSumAllTargets)
    {
        var result = EncounterCatalog.ResolvePveEncounter(encounterId);

        Assert.Equal((short)expectedFightType, result.FightType);
        Assert.Equal(expectedSumAllTargets, result.SumAllTargets);
    }

    [Fact]
    public void ResolvePveEncounter_WhenUnknownEncounter_ReturnsUnknownAndSumsTargets()
    {
        var result = EncounterCatalog.ResolvePveEncounter(-1);

        Assert.Equal((short)FightTypesEnum.Unkn, result.FightType);
        Assert.True(result.SumAllTargets);
    }

    [Theory]
    [InlineData(FightTypesEnum.Spirit, true)]
    [InlineData(FightTypesEnum.Vale, false)]
    [InlineData(FightTypesEnum.Unkn, true)]
    public void ShouldSumAllTargets_ReturnsCatalogTargetMode(FightTypesEnum fightType, bool expected)
    {
        var result = EncounterCatalog.ShouldSumAllTargets((short)fightType);

        Assert.Equal(expected, result);
    }
}
