namespace Models
{
    public class Gw2Player
    {
        public string AccountName { get; set; }

        public string CharacterName { get; set; }

        public string Profession { get; set; }

        public long SubGroup { get; set; }

        public long Damage { get; set; }

        public double Cleanses { get; set; }

        public double Strips { get; set; }

        public double StabUpTime { get; set; }

        public long Healing { get; set; }

        public long Barrier { get; set; }

        public double DistanceFromTag { get; set; }

        public double TimesDowned { get; set; }

        public long Interrupts { get; set; }
    }
}
