using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Handlers.MessageGenerationHandlers;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Models.Enums;

namespace Services.DiscordRequestServices
{
    public class RaffleCommandsService : IRaffleCommandsService
    {
        private readonly DatabaseContext _databaseContext;

        private readonly FooterHandler _footerHandler;

        public RaffleCommandsService(DatabaseContext databaseContext, FooterHandler footerHandler)
        {
            _databaseContext = databaseContext;
            _footerHandler = footerHandler;
        }

        public async Task CreateRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            await command.DeferAsync(ephemeral: true);

            // Get the user who executed the command
            SocketGuildUser? guildUser = null;
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
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to create raffle, please try again, or yell at logan.");
                return;
            }

            // Get the guild from the database
            if (guildUser == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "You don't belong to the discord server, try the command in the discord you want the raffle in!");
                return;
            }

            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Cannot find the discord this should apply to, try the command in the discord you want the raffle in!");
                return;
            }

            // Check if there is already an active raffle
            var raffles = await _databaseContext.Raffle.ToListAsync();
            if (raffles.Any(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId))
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There already is a running raffle, close that one first!");
                return;
            }

            // Get the webhook for the guild
            var webhookUrl = guild.AnnouncementWebhook;
            var webhook = new DiscordWebhookClient(webhookUrl);

            // Build the message for the raffle
            var message = new EmbedBuilder
            {
                Title = "Raffle!\n",
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
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Create the raffle in the database
            var raffle = new Raffle
            {
                Description = $"{command.Data.Options.First().Value}",
                GuildId = guild.GuildId,
                IsActive = true,
                RaffleType = (int)RaffleTypeEnum.Normal
            };

            _databaseContext.Add(raffle);
            await _databaseContext.SaveChangesAsync();

            // Send the message to the webhook
            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message.Build() });

            await command.ModifyOriginalResponseAsync(m => m.Content = "Created!");
        }

        public async Task CreateEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the command execution
            await command.DeferAsync(ephemeral: true);

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
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to create raffle, please try again, or yell at logan.");
                return;
            }

            if (guildUser == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to create raffle, please try again, or yell at logan.");
                return;
            }

            // Get the guild from the database
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);
            
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Cannot find the discord this should apply to, try the command in the discord you want the raffle in!");
                return;
            }

            // Check if there is already an active event raffle
            var raffles = await _databaseContext.Raffle.ToListAsync();
            if (raffles.Any(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId))
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There already is a running raffle, close that one first!");
                return;
            }

            // Get the webhook for the guild
            var webhookUrl = guild.AnnouncementWebhook;
            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Create the raffle in the database
            var raffle = new Raffle
            {
                Description = $"{command.Data.Options.First().Value}",
                GuildId = guild.GuildId,
                IsActive = true,
                RaffleType = (int)RaffleTypeEnum.Event
            };

            _databaseContext.Add(raffle);
            await _databaseContext.SaveChangesAsync();

            // Send the message to the webhook
            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message.Build() });

            await command.ModifyOriginalResponseAsync(m => m.Content = "Created!");
        }

        public async Task RaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Parse the points to spend from the command options
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Please try again and enter a valid number.");
                return;
            }

            // Check if the points to spend is greater than 0
            if (pointsToSpend <= 0)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Need to spend at least 1 point.");
                return;
            }

            // Defer the command execution
            await command.DeferAsync(ephemeral: true);

            // Check if the command was executed in a guild
            if (!command.GuildId.HasValue) {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to create raffle, make sure to use this command within a discord server.");
                return;
            }

            // Get the user who executed the command
            SocketGuildUser? guildUser = discordClient.GetGuild(command.GuildId.Value)?.GetUser(command.User.Id);
            if (guildUser == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to create raffle, please try again, or yell at someone.");
                return;
            }

            // Get the guild from the database
            Guild? guild = await _databaseContext.Guild.FirstOrDefaultAsync(g => g.GuildId == (long)guildUser.Guild.Id);
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "This message does not appear to be a part of a server, are you messaging in a server where the bot is running?");
                return;
            }

            // Get the current active raffle from the database
            Raffle? currentRaffle = await _databaseContext.Raffle.FirstOrDefaultAsync(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
            if (currentRaffle == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There are currently no raffles.");
                return;
            }

            // Get the account of the user from the database
            Account? account = await _databaseContext.Account.FirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id && a.Gw2ApiKey != null);
            if (account == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Could not find an account for you, have you verified?");
                return;
            }

            // Check if the account has enough points to spend
            if (account.AvailablePoints < pointsToSpend)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = $"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.");
                return;
            }

            // Get the current bid of the user from the database
            PlayerRaffleBid? currentBid = await _databaseContext.PlayerRaffleBid.FirstOrDefaultAsync(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
            if (currentBid != null)
            {
                // If the user has already bid, increase the points spent
                currentBid.PointsSpent += pointsToSpend;
                _databaseContext.Update(currentBid);
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

                _databaseContext.Add(currentBid);
            }

            // Decrease the available points of the account
            account.AvailablePoints -= pointsToSpend;
            _databaseContext.Update(account);

            // Save the changes to the database
            await _databaseContext.SaveChangesAsync();

            // Respond to the command with the points added
            await command.ModifyOriginalResponseAsync(m => m.Content = $"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}");
        }

        public async Task EventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Parse the points to spend from the command options
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Please try again and enter a valid number.");
                return;
            }

            // Check if the points to spend is greater than zero
            if (pointsToSpend <= 0)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Need to spend at least 1 point.");
                return;
            }

            // Defer the response to avoid timeouts
            await command.DeferAsync(ephemeral: true);

            // Get the guild user who executed the command
            SocketGuildUser? guildUser;
            try
            {
                guildUser = command.GuildId != null ? discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id) : throw new Exception("No GuildId");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to create raffle, please try again, or yell at someone.");
                return;
            }

            // Get the guild from the database
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // Check if the guild exists
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "This message does not appear to be apart of a server, are you messaging in a server where the bot is running?");
                return;
            }

            // Get the current event raffle
            var raffles = await _databaseContext.Raffle.ToListAsync();
            var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);

            // Check if there is an active event raffle
            if (currentRaffle == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There are currently no raffles.");
                return;
            }

            // Get the account of the user who executed the command
            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            var account = accounts.FirstOrDefault(a => (ulong)a.DiscordId == command.User.Id);

            // Check if the account exists
            if (account == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Could not find an account for you, have you verified?");
                return;
            }

            // Check if the user has enough points to spend
            if (account.AvailablePoints < pointsToSpend)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = $"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.");
                return;
            }

            // Get the current bid of the user for the current event raffle
            var bids = await _databaseContext.PlayerRaffleBid.ToListAsync();
            var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);

            // Update the current bid or create a new one if it doesn't exist
            if (currentBid != null)
            {
                currentBid.PointsSpent += pointsToSpend;
                _databaseContext.Update(currentBid);
            }
            else
            {
                currentBid = new PlayerRaffleBid
                {
                    RaffleId = currentRaffle.Id,
                    DiscordId = account.DiscordId,
                    PointsSpent = pointsToSpend
                };

                _databaseContext.Add(currentBid);
            }

            // Update the available points of the user and save changes to the database
            account.AvailablePoints -= pointsToSpend;
            _databaseContext.Update(account);
            await _databaseContext.SaveChangesAsync();

            // Send a response with the points spent and the total points in the current event raffle
            await command.ModifyOriginalResponseAsync(m => m.Content = $"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}");
        }

        public async Task CompleteRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the response to let the user know the bot is working
            await command.DeferAsync(ephemeral: true);

            // Get the guild user who executed the command
            SocketGuildUser? guildUser;
            try
            {
                guildUser = command.GuildId != null ? discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id) : throw new Exception("No GuildId");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            // Get the guild from the database
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            if (guild == null)
            {
                return;
            }

            // Get the active raffle for the guild
            var raffles = await _databaseContext.Raffle.ToListAsync();
            var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);

            if (currentRaffle == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There are currently no raffles, maybe create one!");
                return;
            }

            // Get all the bids for the current raffle
            var bids = await _databaseContext.PlayerRaffleBid.ToListAsync();
            var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id);

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
                    var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                    account = accounts.FirstOrDefault(a => a.DiscordId == currentRaffleBid.DiscordId);
                    break;
                }
            }

            if (account == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Unable to choose a winner, please try again, or yell at someone");
                return;
            }

            // Get the webhook for the guild
            var webhookUrl = guild.AnnouncementWebhook;
            var webhook = new DiscordWebhookClient(webhookUrl);

            // Build the message to announce the winner
            var message = new EmbedBuilder
            {
                Title = "Raffle!\n",
                Description = $"and the winner is! <@{account.DiscordId}>\n",
                Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
                Author = new EmbedAuthorBuilder()
                {
                    Name = "GW2-DonBot",
                    Url = "https://github.com/LoganWal/GW2-DonBot",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                Timestamp = DateTime.Now
            };

            var builtMessage = message.Build();

            // Set the raffle to inactive
            currentRaffle.IsActive = false;

            _databaseContext.Update(currentRaffle);
            await _databaseContext.SaveChangesAsync();

            // Send the message to the webhook
            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            // Modify the original response to indicate success
            await command.ModifyOriginalResponseAsync(m => m.Content = "Selected!");
        }

        public async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the response to let the user know the command is being processed
            await command.DeferAsync(ephemeral: true);

            // Parse the number of winners from the command options
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var winnersCount))
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Please try again and enter a valid number.");
                return;
            }

            // Check that the number of winners is valid
            if (winnersCount <= 0)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "Must be at least 1 winner.");
                return;
            }

            // Get the guild user who executed the command
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
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            // Get the guild from the database
            Guild? guild;
            Account? account = null;
            Raffle? currentRaffle;
            var guilds = await _databaseContext.Guild.ToListAsync();
            guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            if (guild == null)
            {
                return;
            }

            // Check if there is an active event raffle for the guild
            var raffles = await _databaseContext.Raffle.ToListAsync();
            currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);
            if (currentRaffle != null)
            {
                // Get all the bids for the current raffle
                var bids = await _databaseContext.PlayerRaffleBid.ToListAsync();
                var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id).ToList();
                var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;

                // Pick the winners
                var winners = new List<long>();
                for (int i = 0; i < winnersCount; i++)
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
                            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                            account = accounts.FirstOrDefault(a => a.DiscordId == currentRaffleBid.DiscordId);

                            if (account == null)
                            {
                                await command.ModifyOriginalResponseAsync(m => m.Content = "Unable to choose a winner, please try again, or yell at someone");
                                return;
                            }

                            totalBids -= Convert.ToInt32(currentRaffleBid.PointsSpent);
                            currentRaffleBids.Remove(currentRaffleBid);
                            winners.Add(account.DiscordId);
                            break;
                        }
                    }
                }

                // Build the message to announce the winners
                var webhookUrl = guild.AnnouncementWebhook;
                var webhook = new DiscordWebhookClient(webhookUrl);
                var description = "and the winners are:\n";
                foreach (var (winner, index) in winners.Select((value, i) => (value, i)))
                {
                    description += $"{index + 1}. <@{winner}>\n";
                }

                var message = new EmbedBuilder
                {
                    Title = "Raffle!\n",
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
                        Text = $"{_footerHandler.Generate()}",
                        IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                    },
                    Timestamp = DateTime.Now
                };
                var builtMessage = message.Build();

                // Update the raffle to be inactive
                currentRaffle.IsActive = false;
                _databaseContext.Update(currentRaffle);
                await _databaseContext.SaveChangesAsync();

                // Send the message announcing the winners to the webhook
                await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

                // Modify the original response to indicate that the command was successful
                await command.ModifyOriginalResponseAsync(m => m.Content = "Selected!");
            }
            else
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There are currently no raffles, maybe create one!");
                return;
            }
        }

        public async Task ReopenRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the response to avoid timeouts
            await command.DeferAsync(ephemeral: true);

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
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            // Get the guild
            Guild? guild;
            Raffle? latestRaffle = null;
            var guilds = await _databaseContext.Guild.ToListAsync();
            guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // Check if the guild is null
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "This message does not belong to a discord server, please don't whisper me,");
                return;
            }

            // Check if there is already an open raffle
            var raffles = await _databaseContext.Raffle.ToListAsync();
            if (raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId) != null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There is currently an open raffle.");
                return;
            } 

            // Get the latest raffle
            latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Normal && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);
            if (latestRaffle != null)
            {
                latestRaffle.IsActive = true;
            }
            else
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There is currently no latest raffle, maybe create one!");
                return;
            }

            // Get the webhook
            var webhookUrl = guild.AnnouncementWebhook;
            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            // Update the latest raffle
            _databaseContext.Update(latestRaffle);
            await _databaseContext.SaveChangesAsync();

            // Send the message to the webhook
            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            // Modify the original response to indicate success
            await command.ModifyOriginalResponseAsync(m => m.Content = "Reopened!");
        }

        public async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the response to show that the bot is working
            await command.DeferAsync(ephemeral: true);

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
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(m => m.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            // Get the guild
            Guild? guild;
            var guilds = await _databaseContext.Guild.ToListAsync();
            guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

            // If the guild is null, return an error message
            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "This message does not belong to a discord server, please don't whisper me,");
                return;
            }

            // Check if there is already an open event raffle
            var raffles = await _databaseContext.Raffle.ToListAsync();
            if (raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId) != null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There is currently an open raffle.");
                return;
            }

            // Get the latest event raffle
            var latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Event && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);

            // If there is no latest event raffle, return an error message
            if (latestRaffle == null)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = "There is currently no latest raffle, maybe create one!");
                return;
            }

            // Set the latest event raffle to active
            latestRaffle.IsActive = true;

            // Get the webhook for the guild
            var webhookUrl = guild.AnnouncementWebhook;
            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{_footerHandler.Generate()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            // Update the database with the latest event raffle
            _databaseContext.Update(latestRaffle);
            await _databaseContext.SaveChangesAsync();

            // Send the message to the webhook
            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            // Modify the original response to show that the event raffle has been reopened
            await command.ModifyOriginalResponseAsync(m => m.Content = "Reopened!");
        }
    }
}