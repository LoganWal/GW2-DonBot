namespace DonBot.Services.DiscordServices;

public interface IArtSpamDetector
{
    bool IsSpam(string? content, bool hasImage);
}
