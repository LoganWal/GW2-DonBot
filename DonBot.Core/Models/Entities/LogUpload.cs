using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class LogUpload
{
    [Key]
    public long LogUploadId { get; set; }

    public long DiscordId { get; set; }

    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    [MaxLength(2000)]
    public string? DpsReportUrl { get; set; }

    public long? FightLogId { get; set; }

    [MaxLength(10)]
    public string SourceType { get; set; } = "file";

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public bool SubmitToWingman { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
