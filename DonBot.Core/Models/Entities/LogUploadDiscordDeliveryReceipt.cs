using System.ComponentModel.DataAnnotations;

namespace DonBot.Core.Models.Entities;

public class LogUploadDiscordDeliveryReceipt
{
    [Key]
    public long LogUploadDiscordDeliveryReceiptId { get; set; }

    public long LogUploadId { get; set; }

    [MaxLength(32)]
    public string MessageKind { get; set; } = string.Empty;

    public long? ResolvedChannelId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public long? DiscordMessageId { get; set; }

    [MaxLength(64)]
    public string? FailureCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
