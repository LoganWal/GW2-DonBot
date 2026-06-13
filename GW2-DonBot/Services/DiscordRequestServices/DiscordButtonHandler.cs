using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Scheduling;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.DiscordServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.SchedulerServices;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordRequestServices;

public class DiscordButtonHandler(
    ILogger<DiscordButtonHandler> logger,
    IEntityService entityService,
    IRaffleCommandsService raffleCommandsService,
    IPointsCommandsService pointsCommandsService,
    IFightLogService fightLogService,
    DiscordMessageHandler discordMessageHandler,
    IPendingLogService pendingLogService)
{
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> MessageSemaphores = new();

    public async Task ButtonExecutedAsync(SocketMessageComponent buttonComponent)
    {
        var customId = buttonComponent.Data.CustomId;

        // Keep the event dispatch thread inside Discord's 3-second acknowledgement window.
        if (customId.StartsWith(ButtonId.PostLogsWingmanYesPrefix))
        {
            _ = HandlePostLogsWingmanChoice(buttonComponent, customId, true);
            return;
        }

        if (customId.StartsWith(ButtonId.PostLogsWingmanNoPrefix))
        {
            _ = HandlePostLogsWingmanChoice(buttonComponent, customId, false);
            return;
        }

        if (customId.StartsWith(ButtonId.PostLogsPrefix))
        {
            _ = HandlePostLogs(buttonComponent, customId);
            return;
        }

        if (customId.StartsWith(ButtonId.DismissLogsPrefix))
        {
            _ = HandleDismissLogs(buttonComponent, customId);
            return;
        }

        if (IsScheduledEventButton(customId))
        {
            await HandleScheduledEventButton(buttonComponent, customId);
            return;
        }

        var semaphore = MessageSemaphores.GetOrAdd(buttonComponent.Message.Id, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling button interaction with CustomId: {CustomId}", customId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static bool IsScheduledEventButton(string customId) =>
        customId.StartsWith(ButtonId.ScheduledEventResponsePrefix)
        || customId.StartsWith("join_")
        || customId.StartsWith("cantjoin_")
        || customId.StartsWith("canfill_")
        || customId.StartsWith("willlate_");

    private async Task HandleScheduledEventButton(SocketMessageComponent buttonComponent, string customId)
    {
        try
        {
            await buttonComponent.DeferAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to defer scheduled event button interaction with CustomId: {CustomId}", customId);
            return;
        }

        var semaphore = MessageSemaphores.GetOrAdd(buttonComponent.Message.Id, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            if (customId.StartsWith(ButtonId.ScheduledEventResponsePrefix))
            {
                await HandleEventResponseButton(buttonComponent, customId);
            }
            else if (customId.StartsWith("join_"))
            {
                await HandleLegacyEventButton(buttonComponent, "✅ Roster");
            }
            else if (customId.StartsWith("cantjoin_"))
            {
                await HandleLegacyEventButton(buttonComponent, "❌ Can't Join");
            }
            else if (customId.StartsWith("canfill_"))
            {
                await HandleLegacyEventButton(buttonComponent, "🛠️ Fillers");
            }
            else if (customId.StartsWith("willlate_"))
            {
                await HandleLegacyEventButton(buttonComponent, "⏰ Will Be Late");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling scheduled event button interaction with CustomId: {CustomId}", customId);
            await TryFollowupAsync(buttonComponent, "Something went wrong updating this signup. Please try again.");
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task HandlePostLogs(SocketMessageComponent buttonComponent, string customId)
    {
        try
        {
            var key = customId[ButtonId.PostLogsPrefix.Length..];
            var state = pendingLogService.TryPeek(key);
            if (state == null)
            {
                await buttonComponent.RespondAsync("This log request has expired or was already handled.", ephemeral: true);
                return;
            }

            if (buttonComponent.User.Id != state.UploaderId)
            {
                await buttonComponent.RespondAsync("Only the log uploader can post this summary.", ephemeral: true);
                return;
            }

            var yesButton = new ButtonBuilder()
                .WithLabel("Yes")
                .WithCustomId($"{ButtonId.PostLogsWingmanYesPrefix}{key}")
                .WithStyle(ButtonStyle.Success);
            var noButton = new ButtonBuilder()
                .WithLabel("No")
                .WithCustomId($"{ButtonId.PostLogsWingmanNoPrefix}{key}")
                .WithStyle(ButtonStyle.Secondary);
            var components = new ComponentBuilder()
                .WithButton(yesButton)
                .WithButton(noButton)
                .Build();

            await buttonComponent.UpdateAsync(msg =>
            {
                msg.Content = "Also submit to Wingman?";
                msg.Components = components;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling post logs button: {CustomId}", customId);
        }
    }

    private async Task HandlePostLogsWingmanChoice(SocketMessageComponent buttonComponent, string customId, bool submitToWingman)
    {
        try
        {
            var prefix = submitToWingman ? ButtonId.PostLogsWingmanYesPrefix : ButtonId.PostLogsWingmanNoPrefix;
            var key = customId[prefix.Length..];
            var state = pendingLogService.TryConsume(key);
            if (state == null)
            {
                await buttonComponent.RespondAsync("This log request has expired or was already handled.", ephemeral: true);
                return;
            }

            if (buttonComponent.User.Id != state.UploaderId)
            {
                await buttonComponent.RespondAsync("Only the log uploader can post this summary.", ephemeral: true);
                return;
            }

            await discordMessageHandler.ProcessAndPostLogsAsync(buttonComponent, state, submitToWingman);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling post logs wingman choice button: {CustomId}", customId);
        }
    }

    private async Task HandleDismissLogs(SocketMessageComponent buttonComponent, string customId)
    {
        try
        {
            var key = customId[ButtonId.DismissLogsPrefix.Length..];

            // Do not consume the state until the uploader check passes.
            var state = pendingLogService.TryPeek(key);
            if (state == null)
            {
                await buttonComponent.RespondAsync("This log request has expired or was already handled.", ephemeral: true);
                return;
            }

            if (buttonComponent.User.Id != state.UploaderId)
            {
                await buttonComponent.RespondAsync("Only the log uploader can dismiss this.", ephemeral: true);
                return;
            }

            pendingLogService.TryConsume(key);

            // Type 6 defer acknowledges the interaction without a visible response.
            await buttonComponent.DeferAsync();
            await buttonComponent.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling dismiss logs button: {CustomId}", customId);
        }
    }

    private async Task HandleEventResponseButton(SocketMessageComponent buttonComponent, string customId)
    {
        var payload = customId[ButtonId.ScheduledEventResponsePrefix.Length..];
        var parts = payload.Split('_', 3);
        if (parts.Length < 2
            || !long.TryParse(parts[0], out var scheduledEventId)
            || !int.TryParse(parts[1], out var optionIndex)
            || optionIndex < 0)
        {
            await TryFollowupAsync(buttonComponent, "This event button is invalid.");
            return;
        }

        if (await GetCurrentScheduledEvent(buttonComponent, scheduledEventId) is null)
        {
            return;
        }

        var fieldKey = parts.Length == 3 ? parts[2] : null;
        await ApplyEventButton(buttonComponent, optionIndex, fieldKey);
    }

    private async Task HandleLegacyEventButton(SocketMessageComponent buttonComponent, string fieldName)
    {
        var customId = buttonComponent.Data.CustomId;
        var parts = customId.Split('_', 2);
        if (parts.Length != 2 || !long.TryParse(parts[1], out var scheduledEventId))
        {
            await TryFollowupAsync(buttonComponent, "This event button is invalid.");
            return;
        }

        if (await GetCurrentScheduledEvent(buttonComponent, scheduledEventId) is null)
        {
            return;
        }

        await ApplyEventButton(buttonComponent, fieldName);
    }

    private async Task<ScheduledEvent?> GetCurrentScheduledEvent(SocketMessageComponent buttonComponent, long scheduledEventId)
    {
        var scheduledEvent = await entityService.ScheduledEvent.GetFirstOrDefaultAsync(e => e.ScheduledEventId == scheduledEventId);
        if (scheduledEvent is null || scheduledEvent.MessageId != (long)buttonComponent.Message.Id)
        {
            await TryFollowupAsync(buttonComponent, "This is not the latest event message. Please interact with the most recent message.");
            return null;
        }

        return scheduledEvent;
    }

    private async Task ApplyEventButton(SocketMessageComponent buttonComponent, string fieldName)
    {
        await ApplyEventButton(
            buttonComponent,
            fields => fields.FirstOrDefault(f => f.Name == fieldName));
    }

    private async Task ApplyEventButton(SocketMessageComponent buttonComponent, int fieldIndex, string? fieldKey)
    {
        await ApplyEventButton(
            buttonComponent,
            fields => ResolveResponseField(fields, fieldIndex, fieldKey));
    }

    private static EmbedFieldBuilder? ResolveResponseField(
        IReadOnlyList<EmbedFieldBuilder> fields,
        int fieldIndex,
        string? fieldKey)
    {
        if (!string.IsNullOrWhiteSpace(fieldKey))
        {
            return fields.FirstOrDefault(f => SignupMessageBuilder.FieldKey(f.Name) == fieldKey);
        }

        return fieldIndex < fields.Count ? fields[fieldIndex] : null;
    }

    private async Task ApplyEventButton(
        SocketMessageComponent buttonComponent,
        Func<IReadOnlyList<EmbedFieldBuilder>, EmbedFieldBuilder?> resolveTargetField)
    {
        var user = buttonComponent.User;
        var currentMessage = await GetFreshUserMessageAsync(buttonComponent);
        if (currentMessage is null)
        {
            logger.LogWarning("Could not fetch current message for button interaction.");
            await TryFollowupAsync(buttonComponent, "This event message cannot be updated. Please contact a bot admin.");
            return;
        }

        var embed = currentMessage.Embeds.FirstOrDefault();
        if (embed is null)
        {
            logger.LogWarning("No embed found in the message for button interaction.");
            await TryFollowupAsync(buttonComponent, "This event message cannot be updated. Please contact a bot admin.");
            return;
        }

        var embedBuilder = embed.ToEmbedBuilder();
        var targetField = resolveTargetField(embedBuilder.Fields);
        if (targetField is null)
        {
            await TryFollowupAsync(buttonComponent, "This event button is no longer available.");
            return;
        }

        var targetUsers = GetUserList(targetField);
        if (targetUsers.Any(line => IsSameUserLine(line, user)))
        {
            await TryFollowupAsync(buttonComponent, "You are already in this list.");
            return;
        }

        foreach (var field in embedBuilder.Fields)
        {
            var userList = GetUserList(field);
            var newUserList = userList.Where(line => !IsSameUserLine(line, user)).ToList();

            if (newUserList.Count != userList.Count)
            {
                field.Value = FormatFieldValue(field.Name, newUserList);
            }
        }

        targetUsers = GetUserList(targetField);
        targetUsers.Add(FormatUserLine(user));
        targetField.Value = FormatFieldValue(targetField.Name, targetUsers);

        await currentMessage.ModifyAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
        });

        logger.LogInformation("Updated {FieldName} field for event with user {UserId}", targetField.Name, user.Id);
    }

    private static async Task<IUserMessage?> GetFreshUserMessageAsync(SocketMessageComponent buttonComponent)
    {
        var message = await buttonComponent.Channel.GetMessageAsync(buttonComponent.Message.Id);
        return message as IUserMessage;
    }

    private static List<string> GetUserList(EmbedFieldBuilder field)
    {
        var fieldValue = field.Value as string ?? string.Empty;
        return fieldValue
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line) &&
                           !line.StartsWith("**Total:") &&
                           !GetDefaultFieldValue(field.Name).Equals(line))
            .ToList();
    }

    internal static string FormatUserLine(string mention, string? username)
    {
        var cleanUsername = (username ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(cleanUsername)
            ? mention
            : $"{mention} ({cleanUsername})";
    }

    private static string FormatUserLine(IUser user) =>
        FormatUserLine(user.Mention, user.Username);

    internal static bool IsSameUserLine(string line, ulong userId, string? username)
    {
        var cleanLine = line.Trim();
        return IsUserMentionLine(cleanLine, userId)
               || (!string.IsNullOrWhiteSpace(username)
                   && cleanLine.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameUserLine(string line, IUser user) =>
        IsSameUserLine(line, user.Id, user.Username);

    private static bool IsUserMentionLine(string line, ulong userId) =>
        HasMentionPrefix(line, $"<@{userId}>")
        || HasMentionPrefix(line, $"<@!{userId}>");

    private static bool HasMentionPrefix(string line, string mention) =>
        line.Equals(mention, StringComparison.Ordinal)
        || line.StartsWith($"{mention} ", StringComparison.Ordinal)
        || line.StartsWith($"{mention}(", StringComparison.Ordinal);

    private static async Task TryFollowupAsync(SocketMessageComponent buttonComponent, string message)
    {
        try
        {
            await buttonComponent.FollowupAsync(message, ephemeral: true);
        }
        catch (Exception)
        {
            // Interaction may have expired or already been acknowledged elsewhere.
        }
    }

    private static string FormatFieldValue(string fieldName, List<string> users)
    {
        if (users.Count == 0)
        {
            return GetDefaultFieldValue(fieldName);
        }

        return string.Join('\n', users) + $"\n\n**Total: {users.Count}**";
    }

    private static string GetDefaultFieldValue(string fieldName) =>
        SignupMessageBuilder.GetDefaultFieldValue(fieldName);
}
