using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class GenericCommandsService(IEntityService entityService) : IGenericCommandsService
{
    private static readonly Random Random = new();

    public async Task DigutCommandExecuted(SocketSlashCommand command)
    {
        var now = DateTime.UtcNow;
        var minutesSincePinata = (now.Hour % 2) * 60 + now.Minute;
        var minutesUntilNext = 120 - minutesSincePinata;

        string message;

        if (minutesSincePinata == 0 && now.Second < 30)
        {
            // Pinata is happening RIGHT NOW
            string[] nowMessages =
            [
                "BROOO PINATA IS LIVE RIGHT NOW!!! WHY ARE YOU STILL HERE?? GO GO GO GO!!!",
                "AYO IT'S HAPPENING NOW BRO!!! PINATA IS UP!!! MOVE YOUR LEGS!!!",
                "PINATA NOW BRO!!! I BEEN WAITING FOR THIS ALL DAY!!! LET'S GOOOOO!!!",
                "ITS PINATA TIME RIGHT NOW BRO!!! DROP EVERYTHING AND RUN!!!"
            ];
            message = nowMessages[Random.Next(nowMessages.Length)];
        }
        else if (minutesUntilNext <= 10)
        {
            // Very soon - high alert
            string[] soonMessages =
            [
                $"BRO BRO BRO!! PINATA IN {minutesUntilNext} MINUTES!!! GET READY GET READY!!!",
                $"AYO {minutesUntilNext} MINS LEFT BRO!!! STOP WHAT YOU DOING AND QUEUE UP!!!",
                $"EH EH EH!! {minutesUntilNext} MINUTES TO PINATA!! MY HEART BEATING FAST ALREADY BRO!!!",
                $"CANNOT WAIT BRO!! {minutesUntilNext} MINS!! I ALREADY AT THE SPOT WAITING!!! COME FAST!!!"
            ];
            message = soonMessages[Random.Next(soonMessages.Length)];
        }
        else if (minutesUntilNext <= 30)
        {
            // Getting close - starting to lose it
            string[] closishMessages =
            [
                $"Eh bro, pinata in {minutesUntilNext} mins. Start moving already, don't be late again like last time!!",
                $"Ayo {minutesUntilNext} minutes bro. I'm already getting hype... don't make me go alone again!!",
                $"Got {minutesUntilNext} mins until pinata bro. Chill chill but also... PLEASE DON'T MISS IT!!!",
                $"Bro {minutesUntilNext} mins. Go queue up already, why you still reading this??"
            ];
            message = closishMessages[Random.Next(closishMessages.Length)];
        }
        else if (minutesUntilNext <= 75)
        {
            // Chill but aware
            string[] chillMessages =
            [
                $"Bro I been tracking this since last pinata. {minutesUntilNext} mins. You got time but I'm watching.",
                $"{minutesUntilNext} mins. I'm not stressed. I'm NOT stressed. ...ok maybe a little bit.",
                $"Eh {minutesUntilNext} mins bro. Enough time to afk but not enough time to actually do anything. Just vibe.",
                $"Aiya {minutesUntilNext} mins only. I already at the waypoint just in case. Don't judge me."
            ];
            message = chillMessages[Random.Next(chillMessages.Length)];
        }
        else
        {
            // Long wait - very chill
            string[] longMessages =
            [
                $"Wahhh {minutesUntilNext} mins bro. Long time still. Go eat first, I'll be here waiting.",
                $"Pinata in {minutesUntilNext} mins. Relax bro, got so much time. Don't be like me, standing there 1 hour early.",
                $"Aiya {minutesUntilNext} more minutes. Nothing to worry about yet. Ask me again in a bit.",
                $"Got {minutesUntilNext} mins bro. Take it easy, pinata not going anywhere. Just make sure you show up on time."
            ];
            message = longMessages[Random.Next(longMessages.Length)];
        }

        await command.FollowupAsync(message);
    }


    public async Task AddQuoteCommandExecuted(SocketSlashCommand command)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("This command must be used within a Discord server.", ephemeral: true);
            return;
        }

        var quote = (string)command.Data.Options.First().Value;
        if (string.IsNullOrWhiteSpace(quote))
        {
            await command.FollowupAsync("Quote cannot be empty.", ephemeral: true);
            return;
        }

        await entityService.GuildQuote.AddAsync(new GuildQuote
        {
            GuildId = (long)command.GuildId,
            Quote = quote.Trim()
        });

        await command.FollowupAsync("Quote added!", ephemeral: true);
    }
}