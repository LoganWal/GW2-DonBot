using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.Statics;
using Microsoft.Extensions.Logging;
using System.Text;
using DonBot.Handlers;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices
{
    public class RaffleCommandsService(
        IEntityService entityService,
        FooterHandler footerHandler,
        ILogger<RaffleCommandsService> logger)
        : IRaffleCommandsService
    {
        public async Task CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Get the user who executed the command
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

            // Fetch guild information from the database
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

            // Check for existing active raffles
            var activeRaffleExists = await entityService.Raffle.IfAnyAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);

            if (activeRaffleExists)
            {
                await command.FollowupAsync("There is already a running raffle, close that one first!", ephemeral: true);
                return;
            }

            // Create and send the raffle message
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
                    Text = await footerHandler.Generate(guild.GuildId),
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
            // Get the user who executed the command
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

            // Get the guild from the database
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

            // Check if there is already an active event raffle
            var raffles = await entityService.Raffle.GetAllAsync();
            if (raffles.Any(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId))
            {
                await command.FollowupAsync("There already is a running raffle, close that one first!", ephemeral: true);
                return;
            }

            // Build the message for the raffle
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
                    Text = $"{footerHandler.Generate(guild.GuildId)}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
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

            // Create the raffle in the database
            var raffle = new Raffle
            {
                Description = $"{command.Data.Options.First().Value}",
                GuildId = guild.GuildId,
                IsActive = true,
                RaffleType = (int)RaffleTypeEnum.Event
            };

            await entityService.Raffle.AddAsync(raffle);

            // Send to target channel with components
            await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [message.Build()], components: buttonBuilder);

            await command.FollowupAsync("Created!", ephemeral: true);
        }

        public async Task RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Parse the points to spend from the command options
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
            {
                await command.FollowupAsync("Please try again and enter a valid number.", ephemeral: true);
                return;
            }

            // Check if the points to spend is greater than 0
            if (pointsToSpend <= 0)
            {
                await command.FollowupAsync("Need to spend at least 1 point.", ephemeral: true);
                return;
            }

            // Check if the command was executed in a guild
            if (!command.GuildId.HasValue) {
                await command.FollowupAsync("Failed to create raffle, make sure to use this command within a discord server.", ephemeral: true);
                return;
            }

            // Get the user who executed the command
            var guildUser = discordClient.GetGuild(command.GuildId.Value)?.GetUser(command.User.Id);
            if (guildUser == null)
            {
                await command.FollowupAsync("Failed to create raffle, please try again, or yell at someone.", ephemeral: true);
                return;
            }

            // Get the guild from the database
            var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)guildUser.Guild.Id);
            if (guild == null)
            {
                await command.FollowupAsync("This message does not appear to be a part of a server, are you messaging in a server where the bot is running?", ephemeral: true);
                return;
            }

            // Get the current active raffle from the database
            var currentRaffle = await entityService.Raffle.GetFirstOrDefaultAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
            if (currentRaffle == null)
            {
                await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
                return;
            }

            // Get the account of the user from the database
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

            // Check if the account has enough points to spend
            if (account.AvailablePoints < pointsToSpend)
            {
                await command.FollowupAsync($"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.", ephemeral: true);
                return;
            }

            // Get the current bid of the user from the database
            var currentBid = await entityService.PlayerRaffleBid.GetFirstOrDefaultAsync(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
            if (currentBid != null)
            {
                // If the user has already bid, increase the points spent
                currentBid.PointsSpent += pointsToSpend;
                await entityService.PlayerRaffleBid.UpdateAsync(currentBid);
            }
            else
            {
                // If the user has not bid yet, create a new bid
                currentBid = new PlayerRaffleBid
                {
                    RaffleId = currentRaffle.Id,
                    DiscordId = account.DiscordId,
                    PointsSpent = pointsToSpend
                };

                await entityService.PlayerRaffleBid.AddAsync(currentBid);
            }

            // Decrease the available points of the account
            account.AvailablePoints -= pointsToSpend;
            await entityService.Account.UpdateAsync(account);

            // Respond to the command with the points added
            await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}", ephemeral: true);
        }

        public async Task EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Parse the points to spend from the command options
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
            {
                await command.FollowupAsync("Please try again and enter a valid number.", ephemeral: true);
                return;
            }

            // Check if the points to spend is greater than zero
            if (pointsToSpend <= 0)
            {
                await command.FollowupAsync("Need to spend at least 1 point.", ephemeral: true);
                return;
            }

            // Get the guild user who executed the command
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

            // Get the guild from the database
            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // Check if the guild exists
            if (guild == null)
            {
                await command.FollowupAsync("This message does not appear to be apart of a server, are you messaging in a server where the bot is running?", ephemeral: true);
                return;
            }

            // Get the current event raffle
            var raffles = await entityService.Raffle.GetAllAsync();
            var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);

            // Check if there is an active event raffle
            if (currentRaffle == null)
            {
                await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
                return;
            }

            // Get the account of the user who executed the command
            // Get the account of the user from the database
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

            // Check if the user has enough points to spend
            if (account.AvailablePoints < pointsToSpend)
            {
                await command.FollowupAsync($"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.", ephemeral: true);
                return;
            }

            // Get the current bid of the user for the current event raffle
            var bids = await entityService.PlayerRaffleBid.GetAllAsync();
            var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);

            // Update the current bid or create a new one if it doesn't exist
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

            // Update the available points of the user and save changes to the database
            account.AvailablePoints -= pointsToSpend;
            await entityService.Account.UpdateAsync(account);

            // Send a response with the points spent and the total points in the current event raffle
            await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current event raffle is: {currentBid.PointsSpent}", ephemeral: true);
        }

        public async Task CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Get the guild user who executed the command
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

            // Get the guild from the database
            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            if (guild == null)
            {
                return;
            }

            // Get the active raffle for the guild
            var raffles = await entityService.Raffle.GetAllAsync();
            var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);

            if (currentRaffle == null)
            {
                await command.FollowupAsync("There are currently no raffles, maybe create one!", ephemeral: true);
                return;
            }

            // Get all the bids for the current raffle
            var bids = await entityService.PlayerRaffleBid.GetAllAsync();
            var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id)
                .GroupBy(bid => bid.DiscordId)
                .Select(group => group.First())
                .OrderByDescending(bid => bid.PointsSpent)
                .ToList();

            // Calculate the total number of points spent on the raffle
            var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;

            // Pick a random number between 1 and the total number of points spent
            var random = new Random();
            var pickedBid = random.Next(1, totalBids);

            Account? account = null;
            var rollingTotal = 0m;

            // Loop through the bids until the picked bid is reached
            foreach (var currentRaffleBid in currentRaffleBids)
            {
                rollingTotal += currentRaffleBid.PointsSpent;

                if (pickedBid < rollingTotal)
                {
                    // Get the account associated with the winning bid
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

            // Create a string to hold the top 3 bidders' details
            var topBidders = new StringBuilder("Top 3 Bidders:\n");

            foreach (var bidder in currentRaffleBids.Take(3))
            {
                var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == bidder.DiscordId);
                var accountNames = string.Join(", ", gw2Accounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                topBidders.AppendLine($"<@{bidder.DiscordId}> ({accountNames}) - Bid: {bidder.PointsSpent} points");
            }

            // Get account names for the winning bidder
            var winnerGw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.DiscordId == account.DiscordId);
            var winnerAccountNames = string.Join(", ", winnerGw2Accounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());

            // Build the message to announce the winner
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
                    Text = $"{footerHandler.Generate(guild.GuildId)}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            var builtMessage = message.Build();

            // Set the raffle to inactive
            currentRaffle.IsActive = false;

            await entityService.Raffle.UpdateAsync(currentRaffle);

            // Send the message to the webhook
            await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);

            // Modify the original response to indicate success
            await command.FollowupAsync("Selected!", ephemeral: true);
        }

        public async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Parse the number of winners from the command options
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var winnersCount))
            {
                await command.FollowupAsync("Please try again and enter a valid number.", ephemeral: true);
                return;
            }

            // Check that the number of winners is valid
            if (winnersCount <= 0)
            {
                await command.FollowupAsync("Must be at least 1 winner.", ephemeral: true);
                return;
            }

            // Get the guild user who executed the command
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

            // Get the guild from the database
            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            if (guild == null)
            {
                return;
            }

            var accounts = await entityService.Account.GetAllAsync();
            var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();

            // Check if there is an active event raffle for the guild
            var raffles = await entityService.Raffle.GetAllAsync();
            var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);
            if (currentRaffle != null)
            {
                // Get all the bids for the current raffle
                var bids = await entityService.PlayerRaffleBid.GetAllAsync();
                var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id)
                    .GroupBy(bid => bid.DiscordId)
                    .Select(group => group.First())
                    .OrderByDescending(bid => bid.PointsSpent)
                    .ToList();

                var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;

                // Create a string to hold the top 3 bidders' details
                var topBidders = new StringBuilder("Top 3 Bidders:\n");

                foreach (var bidder in currentRaffleBids.Take(3))
                {
                    var gw2Account = gw2Accounts.Where(s => s.DiscordId == bidder.DiscordId).ToList();
                    var accountNames = string.Join(", ", gw2Account.Where(s => !string.IsNullOrEmpty(s.GuildWarsAccountName)).Select(s => s.GuildWarsAccountName).ToList());
                    topBidders.AppendLine($"<@{bidder.DiscordId}> ({accountNames}) - Bid: {bidder.PointsSpent} points");
                }

                // Pick the winners
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
                            // Get the account of the winner
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

                // Build the message to announce the winners and the top 3 bidders
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
                        Text = $"{footerHandler.Generate(guild.GuildId)}",
                        IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                    },
                    Timestamp = DateTime.Now
                };

                var builtMessage = message.Build();

                // Update the raffle to be inactive
                currentRaffle.IsActive = false;
                await entityService.Raffle.UpdateAsync(currentRaffle);

                // Send the message announcing the winners and top 3 bidders to the webhook
                await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);

                // Modify the original response to indicate that the command was successful
                await command.FollowupAsync("Raffle completed!", ephemeral: true);
            }
            else
            {
                await command.FollowupAsync("There are currently no raffles, maybe create one!", ephemeral: true);
            }
        }

        public async Task ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Get the guild user
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

            // Get the guild
            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // Check if the guild is null
            if (guild == null)
            {
                await command.FollowupAsync("This message does not belong to a discord server, please don't whisper me,", ephemeral: true);
                return;
            }

            // Check if there is already an open raffle
            var raffles = await entityService.Raffle.GetAllAsync();
            if (raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId) != null)
            {
                await command.FollowupAsync("There is currently an open raffle.", ephemeral: true);
                return;
            }

            // Get the latest raffle
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

            // Build the message
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
                    Text = $"{footerHandler.Generate(guild.GuildId)}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            // Update the latest raffle
            await entityService.Raffle.UpdateAsync(latestRaffle);

            // Send the message to the webhook
            await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);

            // Modify the original response to indicate success
            await command.FollowupAsync("Reopened!", ephemeral: true);
        }

        public async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Get the guild user
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

            // Get the guild
            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // If the guild is null, return an error message
            if (guild == null)
            {
                await command.FollowupAsync("This message does not belong to a discord server, please don't whisper me,", ephemeral: true);
                return;
            }

            // Check if there is already an open event raffle
            var raffles = await entityService.Raffle.GetAllAsync();
            if (raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId) != null)
            {
                await command.FollowupAsync("There is currently an open raffle.", ephemeral: true);
                return;
            }

            // Get the latest event raffle
            var latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Event && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);

            // If there is no latest event raffle, return an error message
            if (latestRaffle == null)
            {
                await command.FollowupAsync("There is currently no latest raffle, maybe create one!", ephemeral: true);
                return;
            }

            // Set the latest event raffle to active
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

            // Build the message for the event raffle
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
                    Text = $"{footerHandler.Generate(guild.GuildId)}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            // Update the database with the latest event raffle
            await entityService.Raffle.UpdateAsync(latestRaffle);

            // Send the message to the webhook
            await targetChannel.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", embeds: [builtMessage]);

            // Modify the original response to show that the event raffle has been reopened
            await command.FollowupAsync("Reopened!", ephemeral: true);
        }

        private async Task HandleRaffleEnter(SocketMessageComponent command, int pointsToSpend)
        {
            // Ensure it's a guild-based button interaction
            if (command.Channel is not SocketGuildChannel guildChannel)
            {
                return;
            }

            // Get GuildUser
            var guildUser = guildChannel.GetUser(command.User.Id);

            // Get the guild from the database
            var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)guildUser.Guild.Id);
            if (guild == null)
            {
                await command.FollowupAsync("This message does not appear to be a part of a server, are you messaging in a server where the bot is running?", ephemeral:true);
                return;
            }

            // Get the current active raffle from the database
            var currentRaffle = await entityService.Raffle.GetFirstOrDefaultAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
            if (currentRaffle == null)
            {
                await command.FollowupAsync("There are currently no raffles.", ephemeral: true);
                return;
            }

            // Get the account of the user from the database
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

            // Check if the user has enough points to spend
            if (account.AvailablePoints < pointsToSpend)
            {
                await command.FollowupAsync(errorMessage, ephemeral: true);
                return;
            }

            // Get the current bid of the user from the database
            var currentBid = await entityService.PlayerRaffleBid.GetFirstOrDefaultAsync(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
            if (currentBid != null)
            {
                // If the user has already bid, increase the points spent
                currentBid.PointsSpent += pointsToSpend;
                await entityService.PlayerRaffleBid.UpdateAsync(currentBid);
            }
            else
            {
                // If the user has not bid yet, create a new bid
                currentBid = new PlayerRaffleBid
                {
                    RaffleId = currentRaffle.Id,
                    DiscordId = account.DiscordId,
                    PointsSpent = pointsToSpend
                };

                await entityService.PlayerRaffleBid.AddAsync(currentBid);
            }

            // Decrease the available points of the account
            account.AvailablePoints -= pointsToSpend;
            await entityService.Account.UpdateAsync(account);

            // Respond to the command with the points added
            await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}", ephemeral: true);
        }

        private async Task HandleEventRaffleEnter(SocketMessageComponent command, int pointsToSpend)
        {
            // Ensure it's a guild-based button interaction
            if (command.Channel is not SocketGuildChannel guildChannel)
            {
                return;
            }

            // Get GuildUser
            var guildUser = guildChannel.GetUser(command.User.Id);

            // Get the guild from the database
            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // Check if the guild exists
            if (guild == null)
            {
                await command.FollowupAsync("This message does not appear to be apart of a server, are you messaging in a server where the bot is running?", ephemeral: true);
                return;
            }

            // Get the current event raffle
            var raffles = await entityService.Raffle.GetAllAsync();
            var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);

            // Check if there is an active event raffle
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

            // Check if the user has enough points to spend
            if (account.AvailablePoints < pointsToSpend)
            {
                await command.FollowupAsync(errorMessage, ephemeral: true);
                return;
            }

            // Get the current bid of the user for the current event raffle
            var bids = await entityService.PlayerRaffleBid.GetAllAsync();
            var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);

            // Update the current bid or create a new one if it doesn't exist
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

            // Update the available points of the user and save changes to the database
            account.AvailablePoints -= pointsToSpend;
            await entityService.Account.UpdateAsync(account);

            // Send a response with the points spent and the total points in the current event raffle
            await command.FollowupAsync($"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}", ephemeral: true);
        }
    }
}