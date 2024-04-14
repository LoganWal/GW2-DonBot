namespace Models
{
    public class Gw2Player
    {
        public string AccountName { get; set; }

        public string CharacterName { get; set; }

        public string Profession { get; set; }

        public long SubGroup { get; set; }

        public long Kills { get; set; }

        public long Deaths { get; set; }

        public long Downs { get; set; }

        public double TimesDowned { get; set; }

        public long Damage { get; set; }

        public long DamageDownContribution { get; set; }

        public double Cleanses { get; set; }

        public double Strips { get; set; }

        public double StabUpTime { get; set; }

        public long Healing { get; set; }

        public long BarrierGenerated { get; set; }

        public double DistanceFromTag { get; set; }

        public long Interrupts { get; set; }

        public long NumberOfHitsWhileBlinded { get; set; }

        public double NumberOfMissesAgainst { get; set; }

        public double NumberOfTimesBlockedAttack { get; set; }

        public long NumberOfTimesEnemyBlockedAttack { get; set; }

        public double NumberOfBoonsRipped { get; set; }

        public double DamageTaken { get; set; }

        public double BarrierMitigation { get; set; }

        public double TotalQuick { get; set; }

        public double TotalAlac { get; set; }

        public long CerusOrbsCollected { get; set; }

        public long CerusSpreadHitCount { get; set; }

        public long DeimosOilsTriggered { get; set; }
    }
}
