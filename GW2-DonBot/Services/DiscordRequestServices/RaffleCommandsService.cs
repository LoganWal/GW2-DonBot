using System.Text;
using Discord;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Services.Raffles;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordRequestServices;

public sealed class RaffleCommandsService(
    IEntityService entityService,
    RaffleService raffleService,
    IFooterService footerService,
    ILogger<RaffleCommandsService> logger,
    IConfiguration configuration)
    : IRaffleCommandsService
{
    private sealed class RaffleCommandUserException(string message) : Exception(message);

    private MessageComponent BuildRaffleComponents(int raffleType, long guildId)
    {
        var isEvent = raffleType == (int)RaffleTypeEnum.Event;
        var builder = new ComponentBuilder()
            .WithButton("Points", ButtonId.RafflePoints, ButtonStyle.Success)
            .WithButton("1 Point", isEvent ? ButtonId.RaffleEvent1 : ButtonId.Raffle1)
            .WithButton("50 Points", isEvent ? ButtonId.RaffleEvent50 : ButtonId.Raffle50)
            .WithButton("100 Points", isEvent ? ButtonId.RaffleEvent100 : ButtonId.Raffle100)
            .WithButton("1000 Points", isEvent ? ButtonId.RaffleEvent1000 : ButtonId.Raffle1000, ButtonStyle.Danger)
            .WithButton("Random!", isEvent ? ButtonId.RaffleEventRandom : ButtonId.RaffleRandom, ButtonStyle.Success, row: 1);

        builder.WithButton("Open Raffle Page", style: ButtonStyle.Link, url: BuildRafflePageUrl(guildId), row: 1);

        return builder.Build();
    }

    private string BuildRafflePageUrl(long guildId)
    {
        var webAppBaseUrl = configuration["WebApp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(webAppBaseUrl))
        {
            webAppBaseUrl = configuration["Nuxt:BaseUrl"];
        }
        if (string.IsNullOrWhiteSpace(webAppBaseUrl))
        {
            webAppBaseUrl = "http://localhost:3000";
        }

        return $"{webAppBaseUrl.TrimEnd('/')}/points?guild={guildId}";
    }

    private static ITextChannel ResolveAnnouncementChannel(Guild guild, DiscordSocketClient discordClient)
    {
        if (guild.AnnouncementChannelId == null)
        {
            throw new RaffleCommandUserException("No Announcement Channel Set");
        }

        return discordClient.GetChannel((ulong)guild.AnnouncementChannelId) as ITextChannel
            ?? throw new RaffleCommandUserException("Failed to find the target channel.");
    }

    public async Task CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        SocketGuildUser? guildUser = null;
        if (command.GuildId.HasValue)
        {
            var discordGuild = discordClient.GetGuild(command.GuildId.Value);
            if (discordGuild != null)
            {
                guildUser = discordGuild.GetUser(command.User.Id);
            }
        }

        if (guildUser == null)
        {
            logger.LogError("Failed to create raffle: Guild or user not found.");
            await command.FollowupAsync("Failed to create raffle, please try again or contact support.", ephemeral: true);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            await command.FollowupAsync("Cannot find the related guild, try the command in the guild you want the raffle in!", ephemeral: true);
            return;
        }

        RaffleCreateResult createResult;
        try
        {
            createResult = await raffleService.CreateWithMessageReferenceAsync(
                new RaffleCreateRequest(
                    guild.GuildId,
                    (int)RaffleTypeEnum.Normal,
                    command.Data.Options.First().Value?.ToString(),
                    (long)command.User.Id),
                async (raffle, _) =>
                {
                    var targetChannel = ResolveAnnouncementChannel(guild, discordClient);
                    var message = new EmbedBuilder()
                    {
                        Title = "Raffle!",
                        Description = $"{raffle.Description}{Environment.NewLine}Use /points to check your current points!{Environment.NewLine}Use /enter_raffle <points> to enter!",
                        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = "GW2-DonBot",
                            Url = "https://github.com/LoganWal/GW2-DonBot",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = await footerService.Generate(guild.GuildId),
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Timestamp = DateTime.Now
                    };

                    var sent = await targetChannel.SendMessageAsync(
                        text: $"<@&{guild.DiscordVerifiedRoleId}>",
                        embeds: [message.Build()],
                        components: BuildRaffleComponents(raffle.RaffleType, guild.GuildId));
                    return new RaffleMessageReference(
                        raffle.Id,
                        (long)targetChannel.Id,
                        (long)sent.Id);
                });
        }
        catch (RaffleCommandUserException ex)
        {
            await command.FollowupAsync(ex.Message, ephemeral: true);
            return;
        }
        if (createResult.Status != RaffleOperationStatus.Success)
        {
            await command.FollowupAsync(createResult.Status switch
            {
                RaffleOperationStatus.ActiveRaffleExists => "There is already a running raffle, close that one first!",
                RaffleOperationStatus.DescriptionRequired => "Raffle message is required.",
                _ => "Failed to create raffle, please try again or contact support."
            }, ephemeral: true);
            return;
        }

        await command.FollowupAsync("Raffle created successfully!", ephemeral: true);
    }

    public async Task HandleRaffleButton1(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleRaffleEnter(command, 1);
    }

    public async Task HandleRaffleButton50(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleRaffleEnter(command, 50);
    }

    public async Task HandleRaffleButton100(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleRaffleEnter(command, 100);
    }

    public async Task HandleRaffleButton1000(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleRaffleEnter(command, 1000);
    }

    public async Task HandleRaffleButtonRandom(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleRaffleEnter(command, -1);
    }

    public async Task HandleEventRaffleButton1(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleEventRaffleEnter(command, 1);
    }

    public async Task HandleEventRaffleButton50(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleEventRaffleEnter(command, 50);
    }

    public async Task HandleEventRaffleButton100(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleEventRaffleEnter(command, 100);
    }

    public async Task HandleEventRaffleButton1000(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleEventRaffleEnter(command, 1000);
    }

    public async Task HandleEventRaffleButtonRandom(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);
        await HandleEventRaffleEnter(command, -1);
    }

    public async Task CreateEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        SocketGuildUser? guildUser;
        try
        {
            if (command.GuildId != null)
            {
                guildUser = discordClient.GetGuild(command.GuildId.Value)?.GetUser(command.User.Id);
            }
            else
            {
                throw new Exception("No GuildId");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed create event raffle");
            await command.FollowupAsync("Failed to create raffle, please try again, or yell at logan.", ephemeral: true);
            return;
        }

        if (guildUser == null)
        {
            await command.FollowupAsync("Failed to create raffle, please try again, or yell at logan.", ephemeral: true);
            return;
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            await command.FollowupAsync("Cannot find the discord this should apply to, try the command in the discord you want the raffle in!", ephemeral: true);
            return;
        }

        RaffleCreateResult createResult;
        try
        {
            createResult = await raffleService.CreateWithMessageReferenceAsync(
                new RaffleCreateRequest(
                    guild.GuildId,
                    (int)RaffleTypeEnum.Event,
                    command.Data.Options.First().Value?.ToString(),
                    (long)command.User.Id),
                async (raffle, _) =>
                {
                    var targetChannel = ResolveAnnouncementChannel(guild, discordClient);
                    var message = new EmbedBuilder
                    {
                        Title = "EVENT Raffle!\n",
                        Description = $"{raffle.Description}{Environment.NewLine}Use /points to check your current points!{Environment.NewLine}Use /enter_event_raffle <points> to enter!",
                        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = "GW2-DonBot",
                            Url = "https://github.com/LoganWal/GW2-DonBot",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"{await footerService.Generate(guild.GuildId)}",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Timestamp = DateTime.Now
                    };

                    var sent = await targetChannel.SendMessageAsync(
                        text: $"<@&{guild.DiscordVerifiedRoleId}>",
                        embeds: [message.Build()],
                        components: BuildRaffleComponents(raffle.RaffleType, guild.GuildId));
                    return new RaffleMessageReference(
                        raffle.Id,
                        (long)targetChannel.Id,
                        (long)sent.Id);
                });
        }
        catch (RaffleCommandUserException ex)
        {
            await command.FollowupAsync(ex.Message, ephemeral: true);
            return;
        }
        if (createResult.Status != RaffleOperationStatus.Success)
        {
            await command.FollowupAsync(createResult.Status switch
            {
                RaffleOperationStatus.ActiveRaffleExists => "There already is a running raffle, close that one first!",
                RaffleOperationStatus.DescriptionRequired => "Raffle message is required.",
                _ => "Failed to create raffle, please try again or contact support."
            }, ephemeral: true);
            return;
        }

        await command.FollowupAsync("Created!", ephemeral: true);
    }

    public async Task RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
        {
            await command.FollowupAsync("Please try again and enter a valid number.", ephemeral: true);
            return;
        }

        if (pointsToSpend <= 0)
        {
            await command.FollowupAsync("Need to spend at least 1 point.", ephemeral: true);
            return;
        }

        if (!command.GuildId.HasValue)
        {
            await command.FollowupAsync("Failed to create raffle, make sure to use this command within a discord server.", ephemeral: true);
            return;
        }

        var guildUser = discordClient.GetGuild(command.GuildId.Value)?.GetUser(command.User.Id);
        if (guildUser == null)
        {
            await command.FollowupAsync("Failed to create raffle, please try again, or yell at someone.", ephemeral: true);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)guildUser.Guild.Id);
        if (guild == null)
        {
            await command.FollowupAsync("This message does not appear to be a part of a server, are you messaging in a server where the bot is running?", ephemeral: true);
            return;
        }

        var enterResult = await raffleService.EnterActiveAsync(new RaffleEnterActiveRequest(
            guild.GuildId,
            (int)RaffleTypeEnum.Normal,
            (long)command.User.Id,
            pointsToSpend));

        await command.FollowupAsync(
            FormatEnterResult(enterResult, pointsToSpend, isEvent: false),
            ephemeral: true);
    }

    public async Task EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
        {
            await command.FollowupAsync("Please try again and enter a valid number.", ephemeral: true);
            return;
        }

        if (pointsToSpend <= 0)
        {
            await command.FollowupAsync("Need to spend at least 1 point.", ephemeral: true);
            return;
        }

        SocketGuildUser? guildUser;
        try
        {
            guildUser = command.GuildId != null ? discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id) : throw new Exception("No GuildId");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed enter event raffle");
            await command.FollowupAsync("Failed to create raffle, please try again, or yell at someone.", ephemeral: true);
            return;
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            await command.FollowupAsync("This message does not appear to be apart of a server, are you messaging in a server where the bot is running?", ephemeral: true);
            return;
        }

        var enterResult = await raffleService.EnterActiveAsync(new RaffleEnterActiveRequest(
            guild.GuildId,
            (int)RaffleTypeEnum.Event,
            (long)command.User.Id,
            pointsToSpend));

        await command.FollowupAsync(
            FormatEnterResult(enterResult, pointsToSpend, isEvent: true),
            ephemeral: true);
    }

    public async Task CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        SocketGuildUser? guildUser;
        try
        {
            guildUser = command.GuildId != null ? discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id) : throw new Exception("No GuildId");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed complete raffle");
            await command.FollowupAsync("Failed to end raffle, please try again, or yell at logan.", ephemeral: true);
            return;
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            return;
        }

        RaffleCompleteResult completeResult;
        try
        {
            completeResult = await raffleService.CompleteWithAnnouncementAsync(
                new RaffleCompleteRequest(
                    guild.GuildId,
                    (int)RaffleTypeEnum.Normal,
                    1),
                async (currentRaffle, currentRaffleBids, winners, _) =>
                {
                    var targetChannel = ResolveAnnouncementChannel(guild, discordClient);
                    var winnerBid = winners[0];
                    var topBidders = new StringBuilder("Top 3 Bidders:\n");

                    foreach (var bidder in currentRaffleBids.Take(3))
                    {
                        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == bidder.DiscordId);
                        var accountNames = string.Join(", ", gw2Accounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                        topBidders.AppendLine($"<@{bidder.DiscordId}> ({accountNames}) - Bid: {bidder.PointsSpent} points");
                    }

                    var winnerGw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == winnerBid.DiscordId);
                    var winnerAccountNames = string.Join(", ", winnerGw2Accounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());

                    var message = new EmbedBuilder
                    {
                        Title = "Raffle!\n",
                        Description = $"And the winner is! <@{winnerBid.DiscordId}> ({winnerAccountNames} - Bid: {winnerBid.PointsSpent})\n\n{topBidders}",
                        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = "GW2-DonBot",
                            Url = "https://github.com/LoganWal/GW2-DonBot",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"{await footerService.Generate(guild.GuildId)}",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Timestamp = DateTime.Now
                    };

                    await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [message.Build()]);
                });
        }
        catch (RaffleCommandUserException ex)
        {
            await command.FollowupAsync(ex.Message, ephemeral: true);
            return;
        }

        if (completeResult.Status == RaffleOperationStatus.RaffleNotFound)
        {
            await command.FollowupAsync("There are currently no raffles, maybe create one!", ephemeral: true);
            return;
        }
        if (completeResult.Status != RaffleOperationStatus.Success)
        {
            await command.FollowupAsync("Unable to choose a winner, please try again, or yell at someone", ephemeral: true);
            return;
        }
        await command.FollowupAsync("Selected!", ephemeral: true);
    }

    public async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var winnersCount))
        {
            await command.FollowupAsync("Please try again and enter a valid number.", ephemeral: true);
            return;
        }

        if (winnersCount <= 0)
        {
            await command.FollowupAsync("Must be at least 1 winner.", ephemeral: true);
            return;
        }

        SocketGuildUser? guildUser;
        try
        {
            guildUser = command.GuildId != null
                ? discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id)
                : throw new Exception("No GuildId");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete event raffle");
            await command.FollowupAsync("Failed to end raffle, please try again, or yell at logan.", ephemeral: true);
            return;
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            return;
        }

        RaffleCompleteResult completeResult;
        try
        {
            completeResult = await raffleService.CompleteWithAnnouncementAsync(
                new RaffleCompleteRequest(
                    guild.GuildId,
                    (int)RaffleTypeEnum.Event,
                    winnersCount),
                async (_, currentRaffleBids, winners, _) =>
                {
                    var targetChannel = ResolveAnnouncementChannel(guild, discordClient);
                    var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
                    var topBidders = new StringBuilder("Top 3 Bidders:\n");
                    foreach (var bidder in currentRaffleBids.Take(3))
                    {
                        var gw2Account = gw2Accounts.Where(s => s.DiscordId == bidder.DiscordId).ToList();
                        var accountNames = string.Join(", ", gw2Account.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                        topBidders.AppendLine($"<@{bidder.DiscordId}> ({accountNames}) - Bid: {bidder.PointsSpent} points");
                    }

                    var description = "And the winners are:\n";
                    foreach (var (winner, index) in winners.Select((value, i) => (value, i)))
                    {
                        var gw2Account = gw2Accounts.Where(s => s.DiscordId == winner.DiscordId).ToList();
                        var accountNames = string.Join(", ", gw2Account.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                        description += $"{index + 1}. <@{winner.DiscordId}> ({accountNames}) - Bid: {winner.PointsSpent} points\n";
                    }

                    description += "\n" + topBidders;

                    var message = new EmbedBuilder
                    {
                        Title = "Event Raffle Results!\n",
                        Description = description,
                        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = "GW2-DonBot",
                            Url = "https://github.com/LoganWal/GW2-DonBot",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"{await footerService.Generate(guild.GuildId)}",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Timestamp = DateTime.Now
                    };

                    await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [message.Build()]);
                });
        }
        catch (RaffleCommandUserException ex)
        {
            await command.FollowupAsync(ex.Message, ephemeral: true);
            return;
        }

        if (completeResult.Status == RaffleOperationStatus.RaffleNotFound)
        {
            await command.FollowupAsync("There are currently no raffles, maybe create one!", ephemeral: true);
            return;
        }
        if (completeResult.Status != RaffleOperationStatus.Success)
        {
            await command.FollowupAsync("Unable to choose a winner, please try again, or yell at someone", ephemeral: true);
            return;
        }
        await command.FollowupAsync("Raffle completed!", ephemeral: true);
    }

    public async Task ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        SocketGuildUser? guildUser;
        try
        {
            if (command.GuildId != null)
            {
                guildUser = discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
            }
            else
            {
                throw new Exception("No GuildId");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed reopen raffle");
            await command.FollowupAsync("Failed to end raffle, please try again, or yell at logan.", ephemeral: true);
            return;
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            await command.FollowupAsync("This message does not belong to a discord server, please don't whisper me,", ephemeral: true);
            return;
        }

        RaffleReopenResult reopenResult;
        try
        {
            reopenResult = await raffleService.ReopenWithMessageReferenceAsync(
                new RaffleReopenRequest(
                    guild.GuildId,
                    (int)RaffleTypeEnum.Normal,
                    (long)command.User.Id),
                async (latestRaffle, _) =>
                {
                    var targetChannel = ResolveAnnouncementChannel(guild, discordClient);
                    var message = new EmbedBuilder
                    {
                        Title = "Raffle!\n",
                        Description = $"Reopened last raffle, enter now!{Environment.NewLine}Use /points to check your current points!{Environment.NewLine}Use /enter_raffle <points> to enter!",
                        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = "GW2-DonBot",
                            Url = "https://github.com/LoganWal/GW2-DonBot",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"{await footerService.Generate(guild.GuildId)}",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Timestamp = DateTime.Now
                    };

                    var sent = await targetChannel.SendMessageAsync(
                        text: $"<@&{guild.DiscordVerifiedRoleId}>",
                        embeds: [message.Build()],
                        components: BuildRaffleComponents(latestRaffle.RaffleType, guild.GuildId));
                    return new RaffleMessageReference(
                        latestRaffle.Id,
                        (long)targetChannel.Id,
                        (long)sent.Id);
                });
        }
        catch (RaffleCommandUserException ex)
        {
            await command.FollowupAsync(ex.Message, ephemeral: true);
            return;
        }
        if (reopenResult.Status != RaffleOperationStatus.Success)
        {
            await command.FollowupAsync(reopenResult.Status switch
            {
                RaffleOperationStatus.ActiveRaffleExists => "There is currently an open raffle.",
                RaffleOperationStatus.PreviousRaffleNotFound => "There is currently no latest raffle, maybe create one!",
                _ => "Failed to end raffle, please try again, or yell at logan."
            }, ephemeral: true);
            return;
        }

        await command.FollowupAsync("Reopened!", ephemeral: true);
    }

    public async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        SocketGuildUser? guildUser;
        try
        {
            if (command.GuildId != null)
            {
                guildUser = discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
            }
            else
            {
                throw new Exception("No GuildId");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed reopen event raffle");
            await command.FollowupAsync("Failed to end raffle, please try again, or yell at logan.", ephemeral: true);
            return;
        }

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            await command.FollowupAsync("This message does not belong to a discord server, please don't whisper me,", ephemeral: true);
            return;
        }

        RaffleReopenResult reopenResult;
        try
        {
            reopenResult = await raffleService.ReopenWithMessageReferenceAsync(
                new RaffleReopenRequest(
                    guild.GuildId,
                    (int)RaffleTypeEnum.Event,
                    (long)command.User.Id),
                async (latestRaffle, _) =>
                {
                    var targetChannel = ResolveAnnouncementChannel(guild, discordClient);
                    var message = new EmbedBuilder
                    {
                        Title = "EVENT Raffle!\n",
                        Description = $"Reopened last event raffle, enter now!{Environment.NewLine}Use /points to check your current points!{Environment.NewLine}Use /enter_raffle <points> to enter!",
                        Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = "GW2-DonBot",
                            Url = "https://github.com/LoganWal/GW2-DonBot",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $"{await footerService.Generate(guild.GuildId)}",
                            IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                        },
                        Timestamp = DateTime.Now
                    };

                    var sent = await targetChannel.SendMessageAsync(
                        text: $"<@&{guild.DiscordVerifiedRoleId}>",
                        embeds: [message.Build()],
                        components: BuildRaffleComponents(latestRaffle.RaffleType, guild.GuildId));
                    return new RaffleMessageReference(
                        latestRaffle.Id,
                        (long)targetChannel.Id,
                        (long)sent.Id);
                });
        }
        catch (RaffleCommandUserException ex)
        {
            await command.FollowupAsync(ex.Message, ephemeral: true);
            return;
        }
        if (reopenResult.Status != RaffleOperationStatus.Success)
        {
            await command.FollowupAsync(reopenResult.Status switch
            {
                RaffleOperationStatus.ActiveRaffleExists => "There is currently an open raffle.",
                RaffleOperationStatus.PreviousRaffleNotFound => "There is currently no latest raffle, maybe create one!",
                _ => "Failed to end raffle, please try again, or yell at logan."
            }, ephemeral: true);
            return;
        }

        await command.FollowupAsync("Reopened!", ephemeral: true);
    }

    private async Task HandleRaffleEnter(SocketMessageComponent command, int pointsToSpend)
    {
        if (command.Channel is not SocketGuildChannel guildChannel)
        {
            return;
        }

        var guildUser = guildChannel.GetUser(command.User.Id);
        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)guildUser.Guild.Id);
        if (guild == null)
        {
            await command.FollowupAsync("This message does not appear to be a part of a server, are you messaging in a server where the bot is running?", ephemeral: true);
            return;
        }

        if (pointsToSpend == -1)
        {
            var randomContext = await raffleService.GetRandomEntryContextAsync(
                guild.GuildId,
                (int)RaffleTypeEnum.Normal,
                (long)command.User.Id);
            if (randomContext.Status != RaffleOperationStatus.Success)
            {
                await command.FollowupAsync(FormatRandomEntryContextResult(randomContext), ephemeral: true);
                return;
            }

            pointsToSpend = new Random().Next(1, Convert.ToInt32(Math.Floor(randomContext.AvailablePoints)));
        }

        var enterResult = await raffleService.EnterActiveAsync(new RaffleEnterActiveRequest(
            guild.GuildId,
            (int)RaffleTypeEnum.Normal,
            (long)command.User.Id,
            pointsToSpend));

        await command.FollowupAsync(
            FormatEnterResult(enterResult, pointsToSpend, isEvent: false),
            ephemeral: true);
    }

    private async Task HandleEventRaffleEnter(SocketMessageComponent command, int pointsToSpend)
    {
        if (command.Channel is not SocketGuildChannel guildChannel)
        {
            return;
        }

        var guildUser = guildChannel.GetUser(command.User.Id);

        var guilds = await entityService.Guild.GetAllAsync();
        var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

        if (guild == null)
        {
            await command.FollowupAsync("This message does not appear to be apart of a server, are you messaging in a server where the bot is running?", ephemeral: true);
            return;
        }

        if (pointsToSpend == -1)
        {
            var randomContext = await raffleService.GetRandomEntryContextAsync(
                guild.GuildId,
                (int)RaffleTypeEnum.Event,
                (long)command.User.Id);
            if (randomContext.Status != RaffleOperationStatus.Success)
            {
                await command.FollowupAsync(FormatRandomEntryContextResult(randomContext), ephemeral: true);
                return;
            }

            pointsToSpend = new Random().Next(1, Convert.ToInt32(Math.Floor(randomContext.AvailablePoints)));
        }

        var enterResult = await raffleService.EnterActiveAsync(new RaffleEnterActiveRequest(
            guild.GuildId,
            (int)RaffleTypeEnum.Event,
            (long)command.User.Id,
            pointsToSpend));

        await command.FollowupAsync(
            FormatEnterResult(enterResult, pointsToSpend, isEvent: true),
            ephemeral: true);
    }

    private static string FormatEnterResult(RaffleEnterResult result, decimal pointsToSpend, bool isEvent) =>
        result.Status switch
        {
            RaffleOperationStatus.Success =>
                $"Added {pointsToSpend} points!{Environment.NewLine}Total points in current{(isEvent ? " event" : "")} raffle is: {result.Bid!.PointsSpent}",
            RaffleOperationStatus.RaffleNotFound => "There are currently no raffles.",
            RaffleOperationStatus.AccountNotFound => "Could not find an account for you, have you verified?",
            RaffleOperationStatus.GuildWarsAccountRequired => "Could not find a guild wars 2 account for you, have you verified?",
            RaffleOperationStatus.InsufficientPoints =>
                $"You do not have enough points for that, you currently have {result.AvailablePoints} points to spend.",
            RaffleOperationStatus.InvalidPoints => "Need to spend at least 1 point.",
            _ => "Unable to enter raffle, please try again."
        };

    private static string FormatRandomEntryContextResult(RaffleRandomEntryContextResult result) =>
        result.Status switch
        {
            RaffleOperationStatus.RaffleNotFound => "There are currently no raffles.",
            RaffleOperationStatus.AccountNotFound => "Could not find an account for you, have you verified?",
            RaffleOperationStatus.GuildWarsAccountRequired => "Could not find a guild wars 2 account for you, have you verified?",
            RaffleOperationStatus.InsufficientPoints =>
                $"You do not have enough points for that, you currently have {Convert.ToInt32(Math.Floor(result.AvailablePoints))} points to spend.",
            _ => "Unable to enter raffle, please try again."
        };
}
