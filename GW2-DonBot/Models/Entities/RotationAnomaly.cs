using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonBot.Models.Entities;

[Table("RotationAnomaly")]
public class RotationAnomaly
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    [Required]
    [MaxLength(100)]
    public string AccountName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CharacterName { get; init; } = string.Empty;

    [Required]
    public long SkillId { get; init; }

    [Required]
    [MaxLength(200)]
    public string SkillName { get; init; } = string.Empty;

    [Required]
    public int ConsecutiveCasts { get; init; }

    [Required]
    [Column(TypeName = "decimal(10, 3)")]
    public decimal AverageInterval { get; init; }

    [Required]
    [Column(TypeName = "decimal(10, 3)")]
    public decimal MaxDeviation { get; init; }

    [Required]
    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FightUrl { get; init; } = string.Empty;

    [Required]
    public DateTime DetectedAt { get; init; }
}
