using DonBot.Core.Models.Enums;

namespace DonBot.Core.Services.Raffles;

public static class RaffleRules
{
    public const int MaxDescriptionLength = 4000;

    public static bool IsValidRaffleType(int raffleType) =>
        raffleType is (int)RaffleTypeEnum.Normal or (int)RaffleTypeEnum.Event;

    public static bool IsEventRaffle(int raffleType) =>
        raffleType == (int)RaffleTypeEnum.Event;

    public static string TypeName(int raffleType) =>
        IsEventRaffle(raffleType) ? "Event" : "Normal";

    public static int ResolveWinnersCount(int raffleType, int? requestedCount) =>
        IsEventRaffle(raffleType) ? Math.Max(1, requestedCount ?? 1) : 1;

    public static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxDescriptionLength
            ? trimmed
            : trimmed[..MaxDescriptionLength];
    }
}
