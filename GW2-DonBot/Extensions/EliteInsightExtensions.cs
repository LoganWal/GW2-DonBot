namespace DonBot.Extensions;

public static class EliteInsightExtensions
{
    private static readonly Dictionary<string, string> ClassShorthands = new()
    {
        { "Guardian", "Grd" },
        { "Dragonhunter", "Dh" },
        { "Firebrand", "Fb" },
        { "Willbender", "Wlb" },
        { "Luminary", "Lum" },

        { "Warrior", "War" },
        { "Berserker", "Brs" },
        { "Spellbreaker", "Sb" },
        { "Bladesworn", "Bsw" },
        { "Paragon", "Par" },

        { "Revenant", "Rev" },
        { "Herald", "Her" },
        { "Renegade", "Rgd" },
        { "Vindicator", "Vnd" },
        { "Conduit", "Con" },

        { "Ranger", "Rng" },
        { "Druid", "Drd" },
        { "Soulbeast", "Slb" },
        { "Untamed", "Unt" },
        { "Galeshot", "Gal" },

        { "Thief", "Thf" },
        { "Daredevil", "Dd" },
        { "Deadeye", "Ded" },
        { "Specter", "Spc" },
        { "Antiquary", "Ant" },

        { "Engineer", "Eng" },
        { "Scrapper", "Scr" },
        { "Holosmith", "Hls" },
        { "Mechanist", "Mec" },
        { "Amalgam", "Ama" },

        { "Necromancer", "Nec" },
        { "Reaper", "Rpr" },
        { "Scourge", "Scg" },
        { "Harbinger", "Hrb" },
        { "Ritualist", "Rit" },

        { "Elementalist", "Ele" },
        { "Tempest", "Tmp" },
        { "Weaver", "Wvr" },
        { "Catalyst", "Cat" },
        { "Troubadour", "Tro" },

        { "Mesmer", "Msm" },
        { "Chronomancer", "Chr" },
        { "Mirage", "Mir" },
        { "Virtuoso", "Vrt" },
        { "Evoker", "Evo" },
    };

    public static string GetClassAppend(string? className)
    {
        return string.IsNullOrEmpty(className) ? string.Empty : $" ({ClassShorthands.GetValueOrDefault(className, "???")})";
    }
}