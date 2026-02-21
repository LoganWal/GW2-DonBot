using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class PlayerFightLog
{
    [Key]
    public long PlayerFightLogId { get; init; }

    public long FightLogId { get; init; }

    [MaxLength(1000)]
    public string GuildWarsAccountName { get; init; } = string.Empty;

    public long Damage { get; init; }

    public long Cleave { get; init; }

    public long Kills { get; init; }

    public long Deaths { get; init; }

    public long Downs { get; init; }

    public decimal QuicknessDuration { get; init; }

    public decimal AlacDuration { get; init; }

    public long SubGroup { get; init; }

    public long DamageDownContribution { get; init; }

    public long Cleanses { get; init; }

    public long Strips { get; init; }

    public decimal StabGenOnGroup { get; init; }

    public decimal StabGenOffGroup { get; init; }

    public long Healing { get; init; }

    public long BarrierGenerated { get; init; }

    public decimal DistanceFromTag { get; init; }

    public int TimesDowned { get; init; }

    public long Interrupts { get; init; }

    public long TimesInterrupted { get; init; }

    public long NumberOfHitsWhileBlinded { get; init; }

    public long NumberOfMissesAgainst { get; init; }

    public long NumberOfTimesBlockedAttack { get; init; }

    public long NumberOfTimesEnemyBlockedAttack { get; init; }

    public long NumberOfBoonsRipped { get; init; }

    public long DamageTaken { get; init; }

    public long BarrierMitigation { get; init; }

    public long CerusOrbsCollected { get; init; }

    public long CerusSpreadHitCount { get; init; }

    public decimal CerusPhaseOneDamage { get; init; }

    public long DeimosOilsTriggered { get; init; }

    public int ResurrectionTime { get; init; }

    public long ShardPickUp { get; init; }

    public long ShardUsed { get; init; }
    
    public long? TimeOfDeath { get; init; }
}