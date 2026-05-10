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
        var minutesSinceEven = (now.Hour % 2) * 60 + now.Minute;

        // Pinata starts at :05, peaks around :23, considered over by :30
        // minutesSinceEven: 0-4 = pre-event, 5-22 = live, 23-29 = dead, 30-119 = waiting

        string message;

        if (minutesSinceEven < 5)
        {
            // :00 to :05 - panic trying to find a map before the event
            var minsUntilStart = 5 - minutesSinceEven;
            string[] preEventMessages =
            [
                $"BRO BRO BRO PINATA IN {minsUntilStart} MINS!! WHERE IS THE MAP?? I CANNOT FIND AN OPEN INSTANCE!!!",
                $"EH {minsUntilStart} MINUTES!! I'M SPAMMING THE MAP BUTTON RIGHT NOW!! WHY IS NOTHING LOADING??",
                $"AIYA {minsUntilStart} MINS BRO!! QUICK QUICK!! SOMEONE INVITE ME TO THE MAP PLEASE I BEG!!",
                $"BROOOO {minsUntilStart} MINS LEFT!! I'M RUNNING AROUND LIKE MAD TRYING TO GET IN A MAP!!"
            ];
            message = preEventMessages[Random.Next(preEventMessages.Length)];
        }
        else if (minutesSinceEven < 23)
        {
            // :05 to :23 - pinata is live, losing their mind trying to get into a map
            string[] liveMessages =
            [
                "PINATA IS LIVE BRO!!! I'VE BEEN CLICKING JOIN MAP FOR 5 MINUTES STRAIGHT!!! WHY WON'T IT LET ME IN!!!",
                "IT'S HAPPENING RIGHT NOW AND I CANNOT GET IN THE MAP!!! BRO I'M GOING CRAZY RIGHT NOW!!!",
                "PINATA IS UP!!! I FOUND A MAP BUT IT'S FULL!!! ANOTHER ONE FULL!!! ANOTHER ONE FULL!!! BROOOOO!!!",
                "BRO IT'S LIVE AND I KEEP GETTING KICKED TO OVERFLOW!!! THIS GAME HATES ME!!! JUST LET ME IN!!!"
            ];
            message = liveMessages[Random.Next(liveMessages.Length)];
        }
        else if (minutesSinceEven < 30)
        {
            // :23 to :30 - pinata is dead, you missed it
            string[] deadMessages =
            [
                "Bro... pinata is dead. You missed it. I missed it too. We don't talk about this.",
                "It's over bro. Pinata gone. By the time I found a map it was already dead. RIP.",
                "Aiya too late liao. Pinata down. I was standing there watching it die from overflow. Very pain.",
                "Gone bro. Pinata died. I got into a map but it was already 5% hp. Crushed."
            ];
            message = deadMessages[Random.Next(deadMessages.Length)];
        }
        else
        {
            // :30 to next :00 - chill, waiting for next event at :05 of the next even hour
            var minsUntilNext = 120 - minutesSinceEven + 5;

            if (minsUntilNext <= 10)
            {
                string[] soonMessages =
                [
                    $"BRO BRO BRO!! {minsUntilNext} MINUTES!!! GET READY GET READY!!! FIND YOUR MAP NOW!!!",
                    $"AYO {minsUntilNext} MINS LEFT BRO!!! STOP WHAT YOU DOING AND START LOOKING FOR A MAP!!!",
                    $"EH EH EH!! {minsUntilNext} MINUTES TO PINATA!! MY HEART BEATING FAST ALREADY BRO!!!",
                    $"CANNOT WAIT BRO!! {minsUntilNext} MINS!! I ALREADY SPAMMING THE JOIN BUTTON!!! COME FAST!!!"
                ];
                message = soonMessages[Random.Next(soonMessages.Length)];
            }
            else if (minsUntilNext <= 30)
            {
                string[] closishMessages =
                [
                    $"Eh bro, pinata in {minsUntilNext} mins. Start moving already, don't be late again like last time!!",
                    $"Ayo {minsUntilNext} minutes bro. I'm already getting hype... don't make me go alone again!!",
                    $"Got {minsUntilNext} mins until pinata bro. Chill chill but also... PLEASE DON'T MISS IT!!!",
                    $"Bro {minsUntilNext} mins. Go queue up already, why you still reading this??"
                ];
                message = closishMessages[Random.Next(closishMessages.Length)];
            }
            else if (minsUntilNext <= 75)
            {
                string[] chillMessages =
                [
                    $"Bro I been tracking this since last pinata. {minsUntilNext} mins. You got time but I'm watching.",
                    $"{minsUntilNext} mins. I'm not stressed. I'm NOT stressed. ...ok maybe a little bit.",
                    $"Eh {minsUntilNext} mins bro. Enough time to afk but not enough time to actually do anything. Just vibe.",
                    $"Aiya {minsUntilNext} mins only. I already at the waypoint just in case. Don't judge me."
                ];
                message = chillMessages[Random.Next(chillMessages.Length)];
            }
            else
            {
                string[] longMessages =
                [
                    $"Wahhh {minsUntilNext} mins bro. Long time still. Go eat first, I'll be here waiting.",
                    $"Pinata in {minsUntilNext} mins. Relax bro, got so much time. Don't be like me, standing there 1 hour early.",
                    $"Aiya {minsUntilNext} more minutes. Nothing to worry about yet. Ask me again in a bit.",
                    $"Got {minsUntilNext} mins bro. Take it easy, pinata not going anywhere. Just make sure you show up on time."
                ];
                message = longMessages[Random.Next(longMessages.Length)];
            }
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