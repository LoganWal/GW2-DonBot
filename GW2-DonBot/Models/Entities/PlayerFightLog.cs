using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class PlayerFightLog
{
    [Key]
    public long PlayerFightLogId { get; set; }

    public long FightLogId { get; set; }

    [MaxLength(1000)]
    public string GuildWarsAccountName { get; set; } = string.Empty;

    public long Damage { get; set; }

    public long Cleave { get; set; }

    public long Kills { get; set; }

    public long Deaths { get; set; }

    public long Downs { get; set; }

    public decimal QuicknessDuration { get; set; }

    public decimal AlacDuration { get; set; }

    public long SubGroup { get; set; }

    public long DamageDownContribution { get; set; }

    public long Cleanses { get; set; }

    public long Strips { get; set; }

    public decimal StabGenOnGroup { get; set; }

    public decimal StabGenOffGroup { get; set; }

    public long Healing { get; set; }

    public long BarrierGenerated { get; set; }

    public decimal DistanceFromTag { get; set; }

    public int TimesDowned { get; set; }

    public long Interrupts { get; set; }

    public long TimesInterrupted { get; set; }

    public long NumberOfHitsWhileBlinded { get; set; }

    public long NumberOfMissesAgainst { get; set; }

    public long NumberOfTimesBlockedAttack { get; set; }

    public long NumberOfTimesEnemyBlockedAttack { get; set; }

    public long NumberOfBoonsRipped { get; set; }

    public long DamageTaken { get; set; }

    public long BarrierMitigation { get; set; }

    public long CerusOrbsCollected { get; set; }

    public long CerusSpreadHitCount { get; set; }

    public decimal CerusPhaseOneDamage { get; set; }

    public long DeimosOilsTriggered { get; set; }

    public int ResurrectionTime { get; set; }

    public int FavorUsage { get; set; }

    public int DesertShroudUsage { get; set; }

    public int SandstormShroudUsage { get; set; }

    public int SandFlareUsage { get; set; }
}