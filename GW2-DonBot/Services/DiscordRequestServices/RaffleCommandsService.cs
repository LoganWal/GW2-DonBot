using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.Statics;
using Microsoft.Extensions.Logging;
using System.Text;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Services.DiscordRequestServices;

public sealed class RaffleCommandsService(
    IEntityService entityService,
    IFooterService footerService,
    ILogger<RaffleCommandsService> logger)
    : IRaffleCommandsService
{
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

        if (guild.AnnouncementChannelId == null)
        {
            await command.FollowupAsync("No Announcement Channel Set", ephemeral: true);
            return;
        }

        if (discordClient.GetChannel((ulong)guild.AnnouncementChannelId.Value) is not ITextChannel targetChannel)
        {
            await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
            return;
        }

        var activeRaffleExists = await entityService.Raffle.IfAnyAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);

        if (activeRaffleExists)
        {
            await command.FollowupAsync("There is already a running raffle, close that one first!", ephemeral: true);
            return;
        }

        var message = new EmbedBuilder()
        {
            Title = "Raffle!",
            Description = $"{command.Data.Options.First().Value}{Environment.NewLine}Use /points to check your current points!{Environment.NewLine}Use /enter_raffle <points> to enter!",
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

        var buttonBuilder = new ComponentBuilder()
            .WithButton("Points", ButtonId.RafflePoints, ButtonStyle.Success)
            .WithButton("1 Point", ButtonId.Raffle1)
            .WithButton("50 Points", ButtonId.Raffle50)
            .WithButton("100 Points", ButtonId.Raffle100)
            .WithButton("1000 Points", ButtonId.Raffle1000, ButtonStyle.Danger)
            .WithButton("Random!", ButtonId.RaffleRandom, ButtonStyle.Success)
            .Build();

        var raffle = new Raffle
        {
            Description = $"{command.Data.Options.First().Value}",
            GuildId = guild.GuildId,
            IsActive = true,
            RaffleType = (int)RaffleTypeEnum.Normal
        };

        await entityService.Raffle.AddAsync(raffle);

        await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [message.Build()], components: buttonBuilder);
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

        if (guild.AnnouncementChannelId == null)
        {
            await command.FollowupAsync("No Announcement Channel Set", ephemeral: true);
            return;
        }

        if (discordClient.GetChannel((ulong)guild.AnnouncementChannelId) is not ITextChannel targetChannel)
        {
            await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
            return;
        }

        var raffles = await entityService.Raffle.GetAllAsync();
        if (raffles.Any(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Event } && raf.GuildId == guild.GuildId))
        {
            await command.FollowupAsync("There already is a running raffle, close that one first!", ephemeral: true);
            return;
        }

        var message = new EmbedBuilder
        {
            Title = "EVENT Raffle!\n",
            Description = $"{command.Data.Options.First().Value}{Environment.NewLine}Use /points to check your current points!{Environment.NewLine}Use /enter_event_raffle <points> to enter!",
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

        var buttonBuilder = new ComponentBuilder()
            .WithButton("Points", ButtonId.RafflePoints, ButtonStyle.Success)
            .WithButton("1 Point", ButtonId.RaffleEvent1)
            .WithButton("50 Points", ButtonId.RaffleEvent50)
            .WithButton("100 Points", ButtonId.RaffleEvent100)
            .WithButton("1000 Points", ButtonId.RaffleEvent1000, ButtonStyle.Danger)
            .WithButton("Random!", ButtonId.RaffleEventRandom, ButtonStyle.Success)
            .Build();

        var raffle = new Raffle
        {
            Description = $"{command.Data.Options.First().Value}",
            GuildId = guild.GuildId,
            IsActive = true,
            RaffleType = (int)RaffleTypeEnum.Event
        };

        await entityService.Raffle.AddAsync(raffle);

        await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [message.Build()], components: buttonBuilder);

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

        if (!command.GuildId.HasValue) {
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

        var currentRaffle = await entityService.Raffle.GetFirstOrDefaultAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
        if (currentRaffle == null)
        {
            await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
            return;
        }

        var account = await entityService.Account.GetFirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);
        if (account == null)
        {
            await command.FollowupAsync("Could not find an account for you, have you verified?", ephemeral: true);
            return;
        }

        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == account.DiscordId);
        if (!gw2Accounts.Any())
        {
            await command.FollowupAsync("Could not find a guild wars 2 account for you, have you verified?", ephemeral: true);
            return;
        }

        if (account.AvailablePoints < pointsToSpend)
        {
            await command.FollowupAsync($"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.", ephemeral: true);
            return;
        }

        var currentBid = await entityService.PlayerRaffleBid.GetFirstOrDefaultAsync(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
        if (currentBid != null)
        {
            currentBid.PointsSpent += pointsToSpend;
            await entityService.PlayerRaffleBid.UpdateAsync(currentBid);
        }
        else
        {
            currentBid = new PlayerRaffleBid
            {
                RaffleId = currentRaffle.Id,
                DiscordId = account.DiscordId,
                PointsSpent = pointsToSpend
            };

            await entityService.PlayerRaffleBid.AddAsync(currentBid);
        }

        account.AvailablePoints -= pointsToSpend;
        await entityService.Account.UpdateAsync(account);

        await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}", ephemeral: true);
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

        var raffles = await entityService.Raffle.GetAllAsync();
        var currentRaffle = raffles.FirstOrDefault(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Event } && raf.GuildId == guild.GuildId);

        if (currentRaffle == null)
        {
            await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
            return;
        }

        var account = await entityService.Account.GetFirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);
        if (account == null)
        {
            await command.FollowupAsync("Could not find an account for you, have you verified?", ephemeral: true);
            return;
        }

        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == account.DiscordId);
        if (!gw2Accounts.Any())
        {
            await command.FollowupAsync("Could not find a guild wars 2 account for you, have you verified?", ephemeral: true);
            return;
        }

        if (account.AvailablePoints < pointsToSpend)
        {
            await command.FollowupAsync($"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.", ephemeral: true);
            return;
        }

        var bids = await entityService.PlayerRaffleBid.GetAllAsync();
        var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);

        if (currentBid != null)
        {
            currentBid.PointsSpent += pointsToSpend;
            await entityService.PlayerRaffleBid.UpdateAsync(currentBid);
        }
        else
        {
            currentBid = new PlayerRaffleBid
            {
                RaffleId = currentRaffle.Id,
                DiscordId = account.DiscordId,
                PointsSpent = pointsToSpend
            };

            await entityService.PlayerRaffleBid.AddAsync(currentBid);
        }

        account.AvailablePoints -= pointsToSpend;
        await entityService.Account.UpdateAsync(account);

        await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current event raffle is: {currentBid.PointsSpent}", ephemeral: true);
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

        var raffles = await entityService.Raffle.GetAllAsync();
        var currentRaffle = raffles.FirstOrDefault(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Normal } && raf.GuildId == guild.GuildId);

        if (currentRaffle == null)
        {
            await command.FollowupAsync("There are currently no raffles, maybe create one!", ephemeral: true);
            return;
        }

        var bids = await entityService.PlayerRaffleBid.GetAllAsync();
        var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id)
            .GroupBy(bid => bid.DiscordId)
            .Select(group => group.First())
            .OrderByDescending(bid => bid.PointsSpent)
            .ToList();

        var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;

        var random = new Random();
        var pickedBid = random.Next(1, totalBids);

        Account? account = null;
        var rollingTotal = 0m;

        foreach (var currentRaffleBid in currentRaffleBids)
        {
            rollingTotal += currentRaffleBid.PointsSpent;

            if (pickedBid < rollingTotal)
            {
                account = await entityService.Account.GetFirstOrDefaultAsync(a => a.DiscordId == currentRaffleBid.DiscordId);
                break;
            }
        }

        if (account == null)
        {
            await command.FollowupAsync("Unable to choose a winner, please try again, or yell at someone", ephemeral: true);
            return;
        }

        var winnerBid = currentRaffleBids.FirstOrDefault(s => s.DiscordId == account.DiscordId);

        if (winnerBid == null)
        {
            await command.FollowupAsync("Unable to choose a winner, account chosen didn't have any registered bid", ephemeral: true);
            return;
        }

        if (guild.AnnouncementChannelId == null)
        {
            await command.FollowupAsync("No Announcement Channel Set", ephemeral: true);
            return;
        }

        if (discordClient.GetChannel((ulong)guild.AnnouncementChannelId) is not ITextChannel targetChannel)
        {
            await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
            return;
        }

        var topBidders = new StringBuilder("Top 3 Bidders:\n");

        foreach (var bidder in currentRaffleBids.Take(3))
        {
            var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == bidder.DiscordId);
            var accountNames = string.Join(", ", gw2Accounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
            topBidders.AppendLine($"<@{bidder.DiscordId}> ({accountNames}) - Bid: {bidder.PointsSpent} points");
        }

        var winnerGw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == account.DiscordId);
        var winnerAccountNames = string.Join(", ", winnerGw2Accounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());

        var message = new EmbedBuilder
        {
            Title = "Raffle!\n",
            Description = $"And the winner is! <@{account.DiscordId}> ({winnerAccountNames} - Bid: {winnerBid.PointsSpent})\n\n{topBidders}",
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

        var builtMessage = message.Build();

        currentRaffle.IsActive = false;

        await entityService.Raffle.UpdateAsync(currentRaffle);
        await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);
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

        var accounts = await entityService.Account.GetAllAsync();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();

        var raffles = await entityService.Raffle.GetAllAsync();
        var currentRaffle = raffles.FirstOrDefault(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Event } && raf.GuildId == guild.GuildId);
        if (currentRaffle != null)
        {
            var bids = await entityService.PlayerRaffleBid.GetAllAsync();
            var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id)
                .GroupBy(bid => bid.DiscordId)
                .Select(group => group.First())
                .OrderByDescending(bid => bid.PointsSpent)
                .ToList();

            var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;

            var topBidders = new StringBuilder("Top 3 Bidders:\n");

            foreach (var bidder in currentRaffleBids.Take(3))
            {
                var gw2Account = gw2Accounts.Where(s => s.DiscordId == bidder.DiscordId).ToList();
                var accountNames = string.Join(", ", gw2Account.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                topBidders.AppendLine($"<@{bidder.DiscordId}> ({accountNames}) - Bid: {bidder.PointsSpent} points");
            }

            var winners = new List<Tuple<long, string, decimal>>();
            for (var i = 0; i < winnersCount; i++)
            {
                var random = new Random();
                var pickedBid = random.Next(1, totalBids);

                var rollingTotal = 0m;
                foreach (var currentRaffleBid in currentRaffleBids)
                {
                    rollingTotal += currentRaffleBid.PointsSpent;
                    if (pickedBid < rollingTotal)
                    {
                        var account = accounts.FirstOrDefault(a => a.DiscordId == currentRaffleBid.DiscordId);

                        if (account == null)
                        {
                            await command.FollowupAsync("Unable to choose a winner, please try again, or yell at someone", ephemeral: true);
                            return;
                        }

                        totalBids -= Convert.ToInt32(currentRaffleBid.PointsSpent);
                        currentRaffleBids.Remove(currentRaffleBid);
                        var gw2Account = gw2Accounts.Where(s => s.DiscordId == account.DiscordId).ToList();
                        var accountNames = string.Join(", ", gw2Account.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                        winners.Add(new Tuple<long, string, decimal>(account.DiscordId, accountNames, currentRaffleBid.PointsSpent));
                        break;
                    }
                }
            }

            if (guild.AnnouncementChannelId == null)
            {
                await command.FollowupAsync("No Announcement Channel Set", ephemeral: true);
                return;
            }

            if (discordClient.GetChannel((ulong)guild.AnnouncementChannelId) is not ITextChannel targetChannel)
            {
                await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
                return;
            }

            var description = "And the winners are:\n";
            foreach (var (winner, index) in winners.Select((value, i) => (value, i)))
            {
                description += $"{index + 1}. <@{winner.Item1}> ({winner.Item2}) - Bid: {winner.Item3} points\n";
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

            var builtMessage = message.Build();

            currentRaffle.IsActive = false;
            await entityService.Raffle.UpdateAsync(currentRaffle);
            await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);
            await command.FollowupAsync("Raffle completed!", ephemeral: true);
        }
        else
        {
            await command.FollowupAsync("There are currently no raffles, maybe create one!", ephemeral: true);
        }
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

        var raffles = await entityService.Raffle.GetAllAsync();
        if (raffles.FirstOrDefault(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Normal } && raf.GuildId == guild.GuildId) != null)
        {
            await command.FollowupAsync("There is currently an open raffle.", ephemeral: true);
            return;
        }

        var latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Normal && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);
        if (latestRaffle != null)
        {
            latestRaffle.IsActive = true;
        }
        else
        {
            await command.FollowupAsync("There is currently no latest raffle, maybe create one!", ephemeral: true);
            return;
        }

        if (guild.AnnouncementChannelId == null)
        {
            await command.FollowupAsync("No Announcement Channel Set", ephemeral: true);
            return;
        }

        if (discordClient.GetChannel((ulong)guild.AnnouncementChannelId) is not ITextChannel targetChannel)
        {
            await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
            return;
        }

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

        var builtMessage = message.Build();

        await entityService.Raffle.UpdateAsync(latestRaffle);
        await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);
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

        var raffles = await entityService.Raffle.GetAllAsync();
        if (raffles.FirstOrDefault(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Event } && raf.GuildId == guild.GuildId) != null)
        {
            await command.FollowupAsync("There is currently an open raffle.", ephemeral: true);
            return;
        }

        var latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Event && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);

        if (latestRaffle == null)
        {
            await command.FollowupAsync("There is currently no latest raffle, maybe create one!", ephemeral: true);
            return;
        }

        latestRaffle.IsActive = true;

        if (guild.AnnouncementChannelId == null)
        {
            await command.FollowupAsync("No Announcement Channel Set", ephemeral: true);
            return;
        }

        if (discordClient.GetChannel((ulong)guild.AnnouncementChannelId) is not ITextChannel targetChannel)
        {
            await command.FollowupAsync("Failed to find the target channel.", ephemeral: true);
            return;
        }

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

        var builtMessage = message.Build();

        await entityService.Raffle.UpdateAsync(latestRaffle);
        await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);
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
            await command.FollowupAsync("This message does not appear to be a part of a server, are you messaging in a server where the bot is running?", ephemeral:true);
            return;
        }

        var currentRaffle = await entityService.Raffle.GetFirstOrDefaultAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
        if (currentRaffle == null)
        {
            await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
            return;
        }

        var account = await entityService.Account.GetFirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);
        if (account == null)
        {
            await command.FollowupAsync("Could not find an account for you, have you verified?", ephemeral: true);
            return;
        }

        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == account.DiscordId);
        if (!gw2Accounts.Any())
        {
            await command.FollowupAsync("Could not find a guild wars 2 account for you, have you verified?", ephemeral: true);
            return;
        }

        var errorMessage = $"You do not have enough points for that, you currently have {Convert.ToInt32(Math.Floor(account.AvailablePoints))} points to spend.";
        if (account.AvailablePoints <= 1)
        {
            await command.FollowupAsync(errorMessage, ephemeral: true);
            return;
        }

        if (pointsToSpend == -1)
        {
            pointsToSpend = new Random().Next(1, Convert.ToInt32(Math.Floor(account.AvailablePoints)));
        }

        if (account.AvailablePoints < pointsToSpend)
        {
            await command.FollowupAsync(errorMessage, ephemeral: true);
            return;
        }

        var currentBid = await entityService.PlayerRaffleBid.GetFirstOrDefaultAsync(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
        if (currentBid != null)
        {
            currentBid.PointsSpent += pointsToSpend;
            await entityService.PlayerRaffleBid.UpdateAsync(currentBid);
        }
        else
        {
            currentBid = new PlayerRaffleBid
            {
                RaffleId = currentRaffle.Id,
                DiscordId = account.DiscordId,
                PointsSpent = pointsToSpend
            };

            await entityService.PlayerRaffleBid.AddAsync(currentBid);
        }

        account.AvailablePoints -= pointsToSpend;
        await entityService.Account.UpdateAsync(account);

        await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}", ephemeral: true);
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

        var raffles = await entityService.Raffle.GetAllAsync();
        var currentRaffle = raffles.FirstOrDefault(raf => raf is { IsActive: true, RaffleType: (int)RaffleTypeEnum.Event } && raf.GuildId == guild.GuildId);

        if (currentRaffle == null)
        {
            await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
            return;
        }

        var account = await entityService.Account.GetFirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);
        if (account == null)
        {
            await command.FollowupAsync("Could not find an account for you, have you verified?", ephemeral: true);
            return;
        }

        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == account.DiscordId);
        if (!gw2Accounts.Any())
        {
            await command.FollowupAsync("Could not find a guild wars 2 account for you, have you verified?", ephemeral: true);
            return;
        }

        var errorMessage = $"You do not have enough points for that, you currently have {Convert.ToInt32(Math.Floor(account.AvailablePoints))} points to spend.";
        if (account.AvailablePoints <= 1)
        {
            await command.FollowupAsync(errorMessage, ephemeral: true);
            return;
        }

        if (pointsToSpend == -1)
        {
            pointsToSpend = new Random().Next(1, Convert.ToInt32(Math.Floor(account.AvailablePoints)));
        }

        if (account.AvailablePoints < pointsToSpend)
        {
            await command.FollowupAsync(errorMessage, ephemeral: true);
            return;
        }

        var bids = await entityService.PlayerRaffleBid.GetAllAsync();
        var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);

        if (currentBid != null)
        {
            currentBid.PointsSpent += pointsToSpend;
            await entityService.PlayerRaffleBid.UpdateAsync(currentBid);
        }
        else
        {
            currentBid = new PlayerRaffleBid
            {
                RaffleId = currentRaffle.Id,
                DiscordId = account.DiscordId,
                PointsSpent = pointsToSpend
            };

            await entityService.PlayerRaffleBid.AddAsync(currentBid);
        }

        account.AvailablePoints -= pointsToSpend;
        await entityService.Account.UpdateAsync(account);

        await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}", ephemeral: true);
    }
}