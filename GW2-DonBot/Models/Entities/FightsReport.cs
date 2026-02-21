using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class FightsReport
{
    [Key]
    public long FightsReportId { get; init; }

    public long GuildId { get; init; }

    public DateTime FightsStart { get; init; }

    public DateTime? FightsEnd { get; set; }
}