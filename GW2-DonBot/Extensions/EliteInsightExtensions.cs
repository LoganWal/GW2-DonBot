namespace Extensions
{
    public static class EliteInsightExtensions
    {
        private static readonly Dictionary<string, string> ClassShorthands = new()
        {
            { "Guardian", "Grd" },
            { "Dragonhunter", "Dh" },
            { "Firebrand", "Fb" },
            { "Willbender", "Wlb" },

            { "Warrior", "War" },
            { "Berserker", "Brs" },
            { "Spellbreaker", "Sb" },
            { "Bladesworn", "Bsw" },

            { "Revenant", "Rev" },
            { "Herald", "Her" },
            { "Renegade", "Rgd" },
            { "Vindicator", "Vnd" },

            { "Ranger", "Rng" },
            { "Druid", "Drd" },
            { "Soulbeast", "Slb" },
            { "Untamed", "Unt" },

            { "Thief", "Thf" },
            { "Daredevil", "Dd" },
            { "Deadeye", "Ded" },
            { "Specter", "Spc" },

            { "Engineer", "Eng" },
            { "Scrapper", "Scr" },
            { "Holosmith", "Hls" },
            { "Mechanist", "Mec" },

            { "Necromancer", "Nec" },
            { "Reaper", "Rpr" },
            { "Scourge", "Scg" },
            { "Harbinger", "Hrb" },

            { "Elementalist", "Ele" },
            { "Tempest", "Tmp" },
            { "Weaver", "Wvr" },
            { "Catalyst", "Cat" },

            { "Mesmer", "Msm" },
            { "Chronomancer", "Chr" },
            { "Mirage", "Mir" },
            { "Virtuoso", "Vrt" },
        };

        public static string GetClassShortName(string className)
        {
            return ClassShorthands.GetValueOrDefault(className, "???");
        }

        public static string GetClassAppend(string? className)
        {
            return string.IsNullOrEmpty(className) ? string.Empty : $" ({ClassShorthands.GetValueOrDefault(className, "???")})";
        }
    }
}