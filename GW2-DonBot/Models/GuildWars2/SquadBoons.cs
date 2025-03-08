namespace DonBot.Models.GuildWars2
{
    public class SquadBoons
    {
        public bool Initialized;
        public int PlayerCount;
        public int SquadNumber;
        public float MightStacks;
        public float FuryPercent;
        public float QuickPercent;
        public float AlacrityPercent;
        public float ProtectionPercent;
        public float RegenPercent;
        public float VigorPercent;
        public float AegisPercent;
        public float StabilityPercent;
        public float SwiftnessPercent;
        public float ResistancePercent;
        public float ResolutionPercent;

        public void AverageStats()
        {
            MightStacks /= PlayerCount;
            FuryPercent /= PlayerCount;
            QuickPercent /= PlayerCount;
            AlacrityPercent /= PlayerCount;
            ProtectionPercent /= PlayerCount;
            RegenPercent /= PlayerCount;
            VigorPercent /= PlayerCount;
            AegisPercent /= PlayerCount;
            StabilityPercent /= PlayerCount;
            SwiftnessPercent /= PlayerCount;
            ResistancePercent /= PlayerCount;
            ResolutionPercent /= PlayerCount;
        }
    }
}