using DonBot.Core.Models.Enums;

namespace DonBot.Core.Services.GuildWars2;

public readonly record struct EncounterDefinition(short FightType, bool SumAllTargets);

public static class EncounterCatalog
{
    private static readonly IReadOnlyDictionary<long, EncounterDefinition> ByEncounterId =
        new Dictionary<long, EncounterDefinition>
        {
            [131329] = OneTarget(FightTypesEnum.Vale),
            [131332] = AllTargets(FightTypesEnum.Spirit),
            [131330] = OneTarget(FightTypesEnum.Gorseval),
            [131331] = OneTarget(FightTypesEnum.Sabetha),
            [131585] = OneTarget(FightTypesEnum.Sloth),
            [131586] = OneTarget(FightTypesEnum.Trio),
            [131587] = OneTarget(FightTypesEnum.Matthias),
            [131841] = OneTarget(FightTypesEnum.Escort),
            [131842] = OneTarget(FightTypesEnum.Kc),
            [131843] = OneTarget(FightTypesEnum.Tc),
            [131844] = OneTarget(FightTypesEnum.Xera),
            [132097] = OneTarget(FightTypesEnum.Cairn),
            [132098] = OneTarget(FightTypesEnum.Mo),
            [132099] = OneTarget(FightTypesEnum.Samarog),
            [132100] = OneTarget(FightTypesEnum.Deimos),
            [132353] = OneTarget(FightTypesEnum.Sh),
            [132354] = OneTarget(FightTypesEnum.River),
            [132355] = OneTarget(FightTypesEnum.Bk),
            [132356] = OneTarget(FightTypesEnum.EoS),
            [132357] = OneTarget(FightTypesEnum.SoD),
            [132358] = OneTarget(FightTypesEnum.Dhuum),
            [132609] = OneTarget(FightTypesEnum.Ca),
            [132610] = AllTargets(FightTypesEnum.Largos),
            [132611] = OneTarget(FightTypesEnum.Qadim),
            [132865] = OneTarget(FightTypesEnum.Adina),
            [132866] = OneTarget(FightTypesEnum.Sabir),
            [132867] = OneTarget(FightTypesEnum.Peerless),
            [133121] = OneTarget(FightTypesEnum.Greer),
            [133122] = OneTarget(FightTypesEnum.Decima),
            [133123] = OneTarget(FightTypesEnum.Ura),
            [262657] = AllTargets(FightTypesEnum.Icebrood),
            [262658] = AllTargets(FightTypesEnum.Fraenir),
            [262659] = AllTargets(FightTypesEnum.Kodan),
            [262661] = AllTargets(FightTypesEnum.Whisper),
            [262660] = AllTargets(FightTypesEnum.Boneskinner),
            [262913] = AllTargets(FightTypesEnum.Ah),
            [262914] = AllTargets(FightTypesEnum.Xjj),
            [262915] = AllTargets(FightTypesEnum.Ko),
            [262916] = AllTargets(FightTypesEnum.Ht),
            [262917] = AllTargets(FightTypesEnum.Olc),
            [263425] = AllTargets(FightTypesEnum.Co),
            [263426] = AllTargets(FightTypesEnum.ToF),
            [196865] = AllTargets(FightTypesEnum.Mama),
            [196866] = AllTargets(FightTypesEnum.Siax),
            [196867] = AllTargets(FightTypesEnum.Ensolyss),
            [197121] = AllTargets(FightTypesEnum.Skorvald),
            [197122] = AllTargets(FightTypesEnum.Artsariiv),
            [197123] = AllTargets(FightTypesEnum.Arkk),
            [197378] = AllTargets(FightTypesEnum.AiEle),
            [197379] = AllTargets(FightTypesEnum.AiDark),
            [197377] = AllTargets(FightTypesEnum.AiBoth),
            [197633] = AllTargets(FightTypesEnum.Kanaxai),
            [197890] = AllTargets(FightTypesEnum.Eparch),
            [198145] = AllTargets(FightTypesEnum.Shadow),
            [263681] = AllTargets(FightTypesEnum.Kela)
        };

    public static EncounterDefinition ResolvePveEncounter(long encounterId) =>
        ByEncounterId.TryGetValue(encounterId, out var definition)
            ? definition
            : AllTargets(FightTypesEnum.Unkn);

    public static bool ShouldSumAllTargets(short fightType) =>
        fightType == (short)FightTypesEnum.Unkn ||
        ByEncounterId.Values.Any(definition => definition.FightType == fightType && definition.SumAllTargets);

    private static EncounterDefinition OneTarget(FightTypesEnum fightType) => new((short)fightType, false);

    private static EncounterDefinition AllTargets(FightTypesEnum fightType) => new((short)fightType, true);
}
