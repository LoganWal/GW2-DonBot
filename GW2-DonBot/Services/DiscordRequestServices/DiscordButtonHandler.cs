using Discord;
using Discord.WebSocket;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordRequestServices;

public class DiscordButtonHandler(
    ILogger<DiscordButtonHandler> logger,
    IEntityService entityService,
    IRaffleCommandsService raffleCommandsService,
    IPointsCommandsService pointsCommandsService,
    IFightLogService fightLogService)
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task ButtonExecutedAsync(SocketMessageComponent buttonComponent)
    {
        await Semaphore.WaitAsync();
        try
        {
            var customId = buttonComponent.Data.CustomId;

            if (customId.StartsWith("join_"))
            {
                await HandleEventButton(buttonComponent, "✅ Roster");
            }
            else if (customId.StartsWith("cantjoin_"))
            {
                await HandleEventButton(buttonComponent, "❌ Can't Join");
            }
            else if (customId.StartsWith("canfill_"))
            {
                await HandleEventButton(buttonComponent, "🛠️ Fillers");
            }
            else
            {
                switch (customId)
                {
                    case ButtonId.Raffle1:
                        await raffleCommandsService.HandleRaffleButton1(buttonComponent);
                        break;
                    case ButtonId.Raffle50:
                        await raffleCommandsService.HandleRaffleButton50(buttonComponent);
                        break;
                    case ButtonId.Raffle100:
                        await raffleCommandsService.HandleRaffleButton100(buttonComponent);
                        break;
                    case ButtonId.Raffle1000:
                        await raffleCommandsService.HandleRaffleButton1000(buttonComponent);
                        break;
                    case ButtonId.RaffleRandom:
                        await raffleCommandsService.HandleRaffleButtonRandom(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent1:
                        await raffleCommandsService.HandleEventRaffleButton1(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent50:
                        await raffleCommandsService.HandleEventRaffleButton50(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent100:
                        await raffleCommandsService.HandleEventRaffleButton100(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent1000:
                        await raffleCommandsService.HandleEventRaffleButton1000(buttonComponent);
                        break;
                    case ButtonId.RaffleEventRandom:
                        await raffleCommandsService.HandleEventRaffleButtonRandom(buttonComponent);
                        break;
                    case ButtonId.RafflePoints:
                        await pointsCommandsService.PointsCommandExecuted(buttonComponent);
                        break;
                    case ButtonId.KnowMyEnemy:
                        await fightLogService.GetEnemyInformation(buttonComponent);
                        break;
                    default:
                        if (customId.StartsWith(ButtonId.BestTimesPvEPrefix))
                        {
                            await fightLogService.GetBestTimes(buttonComponent);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling button interaction with CustomId: {CustomId}", buttonComponent.Data.CustomId);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async Task HandleEventButton(SocketMessageComponent buttonComponent, string fieldName)
    {
        var customId = buttonComponent.Data.CustomId;
        var scheduledEventId = long.Parse(customId.Split('_')[1]);

        var scheduledEvent = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(e => e.ScheduledEventId == scheduledEventId);
        if (scheduledEvent is null || scheduledEvent.MessageId != (long)buttonComponent.Message.Id)
        {
            await buttonComponent.RespondAsync("This is not the latest event message. Please interact with the most recent message.", ephemeral: true);
            return;
        }

        var user = buttonComponent.User.Username;

        var embed = buttonComponent.Message.Embeds.FirstOrDefault();
        if (embed is null)
        {
            logger.LogWarning("No embed found in the message for button interaction.");
            return;
        }

        var embedBuilder = embed.ToEmbedBuilder();

        foreach (var field in embedBuilder.Fields)
        {
            var fieldValue = field.Value as string ?? string.Empty;
            var userList = fieldValue
                .Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line) &&
                               !line.StartsWith("**Total:") &&
                               !GetDefaultFieldValue(field.Name).Equals(line))
                .ToList();

            var newUserList = userList.Where(line => !line.Contains(user)).ToList();

            if (newUserList.Count != userList.Count)
            {
                field.Value = FormatFieldValue(field.Name, newUserList);
            }
        }

        var targetField = embedBuilder.Fields.FirstOrDefault(f => f.Name == fieldName);
        if (targetField is not null)
        {
            var targetFieldValue = targetField.Value as string ?? string.Empty;
            var userList = targetFieldValue
                .Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line) &&
                               !line.StartsWith("**Total:") &&
                               !GetDefaultFieldValue(fieldName).Equals(line))
                .ToList();

            if (userList.Any(line => line.Contains(user)))
            {
                await buttonComponent.RespondAsync("You are already in this list.", ephemeral: true);
                return;
            }

            userList.Add(user);
            targetField.Value = FormatFieldValue(fieldName, userList);
        }

        await buttonComponent.UpdateAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
        });

        logger.LogInformation("Updated {FieldName} field for event with user {User}", fieldName, user);
    }

    private static string FormatFieldValue(string fieldName, List<string> users)
    {
        if (users.Count == 0)
        {
            return GetDefaultFieldValue(fieldName);
        }

        return string.Join('\n', users) + $"\n\n**Total: {users.Count}**";
    }

    private static string GetDefaultFieldValue(string fieldName) => fieldName switch
    {
        "✅ Roster" => "No one has joined yet.",
        "❌ Can't Join" => "No one has declined yet.",
        "🛠️ Fillers" => "No fillers yet.",
        _ => string.Empty
    };
}
