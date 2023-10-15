using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using GW2DonBot.Models;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Models.GW2Api;
using Newtonsoft.Json;
using Services.CacheServices;
using Services.DiscordMessagingServices;
using Services.Logging;
using Services.SecretsServices;
using ConnectionState = Discord.ConnectionState;

namespace Controller.Discord
{
    public class DiscordCore: IDiscordCore
    {
        private readonly ISecretService _secretService;
        private readonly ILoggingService _loggingService;
        private readonly ICacheService _cacheService;
        private readonly IMessageGenerationService _messageGenerationService;

        private readonly DiscordSocketClient _client;

        public DiscordCore(ISecretService secretService, ILoggingService loggingService, ICacheService cacheService, IMessageGenerationService messageGenerationService)
        {
            _secretService = secretService;
            _loggingService = loggingService;
            _cacheService = cacheService;
            _messageGenerationService = messageGenerationService;

            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            _client = new DiscordSocketClient(config);
        }

        public async Task MainAsync()
        {
            // Logging in...
            await _client.LoginAsync(TokenType.Bot, _secretService.FetchDonBotToken());
            await _client.StartAsync();

            Console.WriteLine("[DON] GW2-DonBot attempting to connect...");
            while (_client.ConnectionState != ConnectionState.Connected)
            {
                await Task.Delay(100);
            }
            Console.WriteLine("[DON] GW2-DonBot connected in");

            await RegisterCommands(_client);

            _client.MessageReceived += MessageReceivedAsync;
            _client.Log += _loggingService.Log;
            _client.SlashCommandExecuted += SlashCommandExecutedAsync;

            var pollingRolesCancellationToken = new CancellationToken();
            PollingRolesTask(TimeSpan.FromMinutes(30), pollingRolesCancellationToken);

            Console.WriteLine("[DON] GW2-DonBot setup - ready to cause chaos");

            // Block this task until the program is closed.
            var discordCoreCancellationToken = new CancellationToken();
            await Task.Delay(-1, discordCoreCancellationToken);

            // Safely close...
            _client.Log -= _loggingService.Log;
            _client.MessageReceived -= MessageReceivedAsync;
            _client.SlashCommandExecuted -= SlashCommandExecutedAsync;
        }

        private async Task RegisterCommands(DiscordSocketClient client)
        {
            // This only needs to be run if you have made changes
            var guilds = client.Guilds;
            await client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>());
            foreach (var guild in guilds)
            {
                await guild.DeleteApplicationCommandsAsync();

                // Guild commands
                var helpGuildCommand = new SlashCommandBuilder()
                    .WithName("help")
                    .WithDescription("List out DonBot's commands and how to use them.");

                await guild.CreateApplicationCommandAsync(helpGuildCommand.Build());

                var deverifyGuildCommand = new SlashCommandBuilder()
                    .WithName("deverify")
                    .WithDescription("Remove any /verify information stored for your Discord account.");

                await guild.CreateApplicationCommandAsync(deverifyGuildCommand.Build());

                var verifyCommand = new SlashCommandBuilder()
                    .WithName("verify")
                    .WithDescription("Verify your GW2 API key so that your GW2 Account and Discord are linked.")
                    .AddOption("api-key", ApplicationCommandOptionType.String, "The API key you wish to link",
                        isRequired: true);

                await guild.CreateApplicationCommandAsync(verifyCommand.Build());

                var pointsCommand = new SlashCommandBuilder()
                    .WithName("points")
                    .WithDescription("Check how many points you have earned.");

                await guild.CreateApplicationCommandAsync(pointsCommand.Build());

                var createRaffleCommand = new SlashCommandBuilder()
                    .WithName("create_raffle")
                    .WithDescription("Create a raffle.")
                    .AddOption("raffle-description", ApplicationCommandOptionType.String, "The Raffle description",
                        isRequired: true);

                await guild.CreateApplicationCommandAsync(createRaffleCommand.Build());

                var createEventRaffleCommand = new SlashCommandBuilder()
                    .WithName("create_event_raffle")
                    .WithDescription("Create an event raffle.")
                    .AddOption("raffle-description", ApplicationCommandOptionType.String, "The Raffle description",
                        isRequired: true);

                await guild.CreateApplicationCommandAsync(createEventRaffleCommand.Build());

                var raffleCommand = new SlashCommandBuilder()
                    .WithName("enter_raffle")
                    .WithDescription("RAFFLE TIME.")
                    .AddOption("points-to-spend", ApplicationCommandOptionType.Integer,
                        "How many points do you want to spend?", isRequired: true);

                await guild.CreateApplicationCommandAsync(raffleCommand.Build());

                var eventRaffleCommand = new SlashCommandBuilder()
                    .WithName("enter_event_raffle")
                    .WithDescription("RAFFLE TIME.")
                    .AddOption("points-to-spend", ApplicationCommandOptionType.Integer,
                        "How many points do you want to spend?", isRequired: true);

                await guild.CreateApplicationCommandAsync(eventRaffleCommand.Build());

                var completeRaffleCommand = new SlashCommandBuilder()
                    .WithName("complete_raffle")
                    .WithDescription("Complete the raffle.");

                await guild.CreateApplicationCommandAsync(completeRaffleCommand.Build());

                var completeEventRaffleCommand = new SlashCommandBuilder()
                    .WithName("complete_event_raffle")
                    .WithDescription("Complete the event raffle.")
                    .AddOption("how-many-winners", ApplicationCommandOptionType.Integer,
                        "How many winners for the event raffle?", isRequired: true);

                await guild.CreateApplicationCommandAsync(completeEventRaffleCommand.Build());

                var redrawRaffleCommand = new SlashCommandBuilder()
                    .WithName("reopen_raffle")
                    .WithDescription("Reopen the raffle.");

                await guild.CreateApplicationCommandAsync(redrawRaffleCommand.Build());

                var redrawEventRaffleCommand = new SlashCommandBuilder()
                    .WithName("reopen_event_raffle")
                    .WithDescription("Reopen the event raffle.");

                await guild.CreateApplicationCommandAsync(redrawEventRaffleCommand.Build());
            }
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case        "help":                   await HelpCommandExecuted(command);                break;
                case        "verify":                 await VerifyCommandExecuted(command);              break;
                case        "deverify":               await DeverifyCommandExecuted(command);            break;
                case        "points":                 await PointsCommandExecuted(command);              break;
                case        "create_raffle":          await CreateRaffleCommandExecuted(command);        break;
                case        "create_event_raffle":    await CreateEventRaffleCommandExecuted(command);   break;
                case        "enter_raffle":           await RaffleCommandExecuted(command);              break;
                case        "enter_event_raffle":     await EventRaffleCommandExecuted(command);         break;
                case        "complete_raffle":        await CompleteRaffleCommandExecuted(command);      break;
                case        "complete_event_raffle":  await CompleteEventRaffleCommandExecuted(command); break;
                case        "reopen_raffle":          await ReopenRaffleCommandExecuted(command);        break;
                case        "reopen_event_raffle":    await ReopenEventRaffleCommandExecuted(command);   break;
                default:                              await DefaultCommandExecuted(command);             break;
            }
        }

        private async Task HelpCommandExecuted(SocketSlashCommand command)
        {
            var message = "";
            message += "**/help**";
            message += "\n*The output of this command will only be visible to you.*";
            message += "\nThis is where you are now! Use this to get help on how some commands work.";

            message += "\n\n**/verify**";
            message += "\n*The output of this command will only be visible to you.*";
            message += "\nThis command can be used to link your GW2 and Discord accounts via a GW2 API key! ";
            message += "This is required to have access to some roles, and will give you access to future features once they're developed! ";
            message += "Once verified, you won't need to use this command again unless you wish to update your details.";
            message += "\n`[api-key]` This is your GW2 API key, make sure it has guild and account permissions!";

            message += "\n\n**/deverify**";
            message += "\n*The output of this command will only be visible to you.*";
            message += "\nThis command can be used to remove any currently stored data associated with your Discord account. ";
            message += "The data stored via the /verify command can be wiped through this. Note you will have to re-verify to access certain roles and features! ";
            message += "This will only remove the information associated with the Discord account used to trigger the command.";

            await command.RespondAsync(message, ephemeral: true);
        }

        private async Task ReopenRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            Guild? guild;
            Raffle? latestRaffle = null;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "This message does not belong to a discord server, please don't whisper me,");
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();

                if (raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId) != null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There is currently an open raffle.");
                    return;
                } 

                latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Normal && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);
                if (latestRaffle != null)
                {
                    latestRaffle.IsActive = true;
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There is currently no latest raffle, maybe create one!");
                    return;
                }
            }

            var webhookUrl = guild.AnnouncementWebhook;

            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{MessageGenerationService.GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                context.Update(latestRaffle);
                await context.SaveChangesAsync();
            }

            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            await command.ModifyOriginalResponseAsync(message => message.Content = "Reopened!");
        }

        private async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            Guild? guild;
            Raffle? latestRaffle = null;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "This message does not belong to a discord server, please don't whisper me,");
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();

                if (raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId) != null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There is currently an open raffle.");
                    return;
                }

                latestRaffle = raffles.Where(raffle => raffle.RaffleType == (int)RaffleTypeEnum.Event && raffle.GuildId == guild.GuildId).MaxBy(raffle => raffle.Id);
                if (latestRaffle != null)
                {
                    latestRaffle.IsActive = true;
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There is currently no latest raffle, maybe create one!");
                    return;
                }
            }

            var webhookUrl = guild.AnnouncementWebhook;

            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{MessageGenerationService.GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                context.Update(latestRaffle);
                await context.SaveChangesAsync();
            }

            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            await command.ModifyOriginalResponseAsync(message => message.Content = "Reopened!");
        }

        private async Task CompleteRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            Guild? guild;
            Account? account = null;
            Raffle? currentRaffle;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();
                currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
                if (currentRaffle != null)
                {
                    var bids = await context.PlayerRaffleBid.ToListAsync();
                    var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id);
                    var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;
                    var random = new Random();
                    var pickedBid = random.Next(1, totalBids);

                    var rollingTotal = 0m;
                    foreach (var currentRaffleBid in currentRaffleBids)
                    {
                        rollingTotal += currentRaffleBid.PointsSpent;
                        if (pickedBid < rollingTotal)
                        {
                            var accounts = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                            account = accounts.FirstOrDefault(a => a.DiscordId == currentRaffleBid.DiscordId);
                            break;
                        }
                    }
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There are currently no raffles, maybe create one!");
                    return;
                }
            }

            if (account == null || currentRaffle == null)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Unable to choose a winner, please try again, or yell at someone");
                return;
            }

            var webhookUrl = guild.AnnouncementWebhook;

            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{MessageGenerationService.GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                currentRaffle.IsActive = false;

                context.Update(currentRaffle);
                await context.SaveChangesAsync();
            }

            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            await command.ModifyOriginalResponseAsync(message => message.Content = "Selected!");
        }

        private async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var winnersCount))
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Please try again and enter a valid number.");
                return;
            }

            if (winnersCount <= 0)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Must be at least 1 winner.");
                return;
            }

            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to end raffle, please try again, or yell at logan.");
                return;
            }

            Guild? guild;
            Account? account = null;
            Raffle? currentRaffle;
            var winners = new List<long>();
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();
                currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);
                if (currentRaffle != null)
                {
                    var bids = await context.PlayerRaffleBid.ToListAsync();
                    var currentRaffleBids = bids.Where(bid => bid.RaffleId == currentRaffle.Id).ToList();
                    var totalBids = Convert.ToInt32(currentRaffleBids.Sum(bid => bid.PointsSpent)) + 1;

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
                                var accounts = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                                account = accounts.FirstOrDefault(a => a.DiscordId == currentRaffleBid.DiscordId);

                                if (account == null)
                                {
                                    await command.ModifyOriginalResponseAsync(message => message.Content = "Unable to choose a winner, please try again, or yell at someone");
                                    return;
                                }

                                totalBids -= 1;
                                currentRaffleBids.Remove(currentRaffleBid);
                                winners.Add(account.DiscordId);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There are currently no raffles, maybe create one!");
                    return;
                }
            }

            if (currentRaffle == null)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Unable to choose a winner, no existing raffle, please try again, or yell at someone");
                return;
            }

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
                    Text = $"{MessageGenerationService.GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                currentRaffle.IsActive = false;

                context.Update(currentRaffle);
                await context.SaveChangesAsync();
            }

            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            await command.ModifyOriginalResponseAsync(message => message.Content = "Selected!");
        }

        private async Task RaffleCommandExecuted(SocketSlashCommand command)
        {
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Please try again and enter a valid number.");
                return;
            }

            if (pointsToSpend <= 0)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Need to spend at least 1 point.");
                return;
            }

            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to create raffle, please try again, or yell at someone.");
                return;
            }

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "This message does not appear to be apart of a server, are you messaging in a server where the bot is running?");
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();
                var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId);
                if (currentRaffle != null)
                {
                    var accounts = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                    var account = accounts.FirstOrDefault(a => (ulong)a.DiscordId == command.User.Id);
                    if (account != null)
                    {
                        if (account.AvailablePoints >= pointsToSpend)
                        {
                            var bids = await context.PlayerRaffleBid.ToListAsync();
                            var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
                            if (currentBid != null)
                            {
                                currentBid.PointsSpent += pointsToSpend;
                                context.Update(currentBid);
                            }
                            else
                            {
                                currentBid = new PlayerRaffleBid
                                {
                                    RaffleId = currentRaffle.Id,
                                    DiscordId = account.DiscordId,
                                    PointsSpent = pointsToSpend
                                };

                                context.Add(currentBid);
                            }

                            account.AvailablePoints -= pointsToSpend;

                            context.Update(account);
                            await context.SaveChangesAsync();

                            await command.ModifyOriginalResponseAsync(message => message.Content = $"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}");
                        }
                        else
                        {
                            await command.ModifyOriginalResponseAsync(message => message.Content = $"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.");
                        }
                    }
                    else
                    {
                        await command.ModifyOriginalResponseAsync(message => message.Content = "Could not find an account for you, have you verified?");
                    }
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There are currently no raffles.");
                }
            }
        }

        private async Task EventRaffleCommandExecuted(SocketSlashCommand command)
        {
            if (!int.TryParse(command.Data.Options.First().Value.ToString(), out var pointsToSpend))
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Please try again and enter a valid number.");
                return;
            }

            if (pointsToSpend <= 0)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Need to spend at least 1 point.");
                return;
            }

            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to create raffle, please try again, or yell at someone.");
                return;
            }

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "This message does not appear to be apart of a server, are you messaging in a server where the bot is running?");
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();
                var currentRaffle = raffles.FirstOrDefault(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId);
                if (currentRaffle != null)
                {
                    var accounts = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                    var account = accounts.FirstOrDefault(a => (ulong)a.DiscordId == command.User.Id);
                    if (account != null)
                    {
                        if (account.AvailablePoints >= pointsToSpend)
                        {
                            var bids = await context.PlayerRaffleBid.ToListAsync();
                            var currentBid = bids.FirstOrDefault(bid => bid.RaffleId == currentRaffle.Id && bid.DiscordId == account.DiscordId);
                            if (currentBid != null)
                            {
                                currentBid.PointsSpent += pointsToSpend;
                                context.Update(currentBid);
                            }
                            else
                            {
                                currentBid = new PlayerRaffleBid
                                {
                                    RaffleId = currentRaffle.Id,
                                    DiscordId = account.DiscordId,
                                    PointsSpent = pointsToSpend
                                };

                                context.Add(currentBid);
                            }

                            account.AvailablePoints -= pointsToSpend;

                            context.Update(account);
                            await context.SaveChangesAsync();

                            await command.ModifyOriginalResponseAsync(message => message.Content = $"Added {pointsToSpend} points!{Environment.NewLine}Total points in current raffle is: {currentBid.PointsSpent}");
                        }
                        else
                        {
                            await command.ModifyOriginalResponseAsync(message => message.Content = $"You do not have enough points for that, you currently have {account.AvailablePoints} points to spend.");
                        }
                    }
                    else
                    {
                        await command.ModifyOriginalResponseAsync(message => message.Content = "Could not find an account for you, have you verified?");
                    }
                }
                else
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There are currently no raffles.");
                }
            }
        }

        private async Task CreateRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to create raffle, please try again, or yell at logan.");
                return;
            }

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "Cannot find the discord this should apply to, try the command in the discord you want the raffle in!");
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();
                if (raffles.Any(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Normal && raf.GuildId == guild.GuildId))
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There already is a running raffle, close that one first!");
                    return;
                }
            }

            var webhookUrl = guild.AnnouncementWebhook;

            var webhook = new DiscordWebhookClient(webhookUrl);

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
                    Text = $"{MessageGenerationService.GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var raffle = new Raffle
                {
                    Description = $"{command.Data.Options.First().Value}",
                    GuildId = guild.GuildId,
                    IsActive = true,
                    RaffleType = (int)RaffleTypeEnum.Normal
                };

                context.Add(raffle);
                await context.SaveChangesAsync();
            }

            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            await command.ModifyOriginalResponseAsync(message => message.Content = "Created!");
        }

        private async Task CreateEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing raffle create nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to create raffle, please try again, or yell at logan.");
                return;
            }

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Guild.Id);

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "Cannot find the discord this should apply to, try the command in the discord you want the raffle in!");
                    return;
                }

                var raffles = await context.Raffle.ToListAsync();
                if (raffles.Any(raf => raf.IsActive && raf.RaffleType == (int)RaffleTypeEnum.Event && raf.GuildId == guild.GuildId))
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = "There already is a running raffle, close that one first!");
                    return;
                }
            }

            var webhookUrl = guild.AnnouncementWebhook;

            var webhook = new DiscordWebhookClient(webhookUrl);

            var message = new EmbedBuilder
            {
                Title = "EVENT Raffle!\n",
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
                    Text = $"{MessageGenerationService.GetJokeFooter()}",
                    IconUrl = "https://i.imgur.com/tQ4LD6H.png"
                },
                // Timestamp
                Timestamp = DateTime.Now
            };

            // Building the message for use
            var builtMessage = message.Build();

            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var raffle = new Raffle
                {
                    Description = $"{command.Data.Options.First().Value}",
                    GuildId = guild.GuildId,
                    IsActive = true,
                    RaffleType = (int)RaffleTypeEnum.Event
                };

                context.Add(raffle);
                await context.SaveChangesAsync();
            }

            await webhook.SendMessageAsync(text: $"<@&{guild.DiscordVerifiedRoleId}>", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { builtMessage });

            await command.ModifyOriginalResponseAsync(message => message.Content = "Created!");
        }

        private async Task VerifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                { 
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Failing verify nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to verify, please try again.");
                return;
            }

            string? apiKey = null;
            try
            {
                if (command.Data.Options != null)
                {
                    apiKey = command.Data.Options.First().Value.ToString();
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("No apiKey provided");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing verify nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to verify, please try again.");
                return;
            }

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[DON] API call success");

                var stringData = await response.Content.ReadAsStringAsync();
                var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                var isNewAccount = false;

                Guild? guild;
                using (var context = new DatabaseContext().SetSecretService(_secretService))
                {
                    var accounts = await context.Account.ToListAsync();

                    var account = accounts.FirstOrDefault(a => (ulong)a.DiscordId == command.User.Id);
                    
                    if (account != null)
                    {
                        account.Gw2AccountId = accountData.Id;
                        account.Gw2AccountName = accountData.Name;
                        account.Gw2ApiKey = apiKey;

                        context.Update(account);
                    }
                    else
                    {
                        isNewAccount = true;
                        account = new Account() 
                        {
                            DiscordId = (long)command.User.Id,
                            Gw2AccountId = accountData.Id,
                            Gw2AccountName = accountData.Name,
                            Gw2ApiKey = apiKey
                        };
                        context.Add(account);
                    }

                    await context.SaveChangesAsync();

                    var guilds = await context.Guild.ToListAsync();
                    guild = guilds.FirstOrDefault(g => g.GuildId == (long)command.GuildId);

                    if (guild == null)
                    {
                        return;
                    }
                }

                var output = "";
                output += isNewAccount ?
                          $"Verify succeeded! New GW2 account registered: `{accountData.Name}`\n" :
                          $"Verify succeeded! GW2 account updated: `{accountData.Name}`\n";

                output += "Verify role has been assigned!\n";

                var primaryGuildId = guild.Gw2GuildMemberRoleId;
                var secondaryGuildIds = guild.Gw2SecondaryMemberRoleIds.Split(',');
                
                var inPrimaryGuild = accountData.Guilds.Contains(primaryGuildId);
                var inSecondaryGuild = secondaryGuildIds.Any(guildId => accountData.Guilds.Contains(guildId));

                output += inPrimaryGuild ?
                          "User is in `Standard of Heroes` - SoX roles have been assigned! :heart:" :
                          inSecondaryGuild ?
                          "User is in an Alliance guild - Alliance roles have been assigned! :heart:" :
                          "User is not in `Standard of Heroes` or a valid Alliance guild - special roles denied! :broken_heart:\nPlease contact Squirrel or an officer if this is incorrect!";
                
                // Adds verified role
                var primaryRoleId = guild.DiscordGuildMemberRoleId;
                var secondaryRoleId = guild.DiscordSecondaryMemberRoleId;
                var verifiedRoleId = guild.DiscordVerifiedRoleId;
                var primaryRole = ((IGuildChannel)command.Channel).Guild.GetRole((ulong)primaryRoleId);
                var secondaryRole = ((IGuildChannel)command.Channel).Guild.GetRole((ulong)secondaryRoleId);
                var verifiedRole = ((IGuildChannel)command.Channel).Guild.GetRole((ulong)verifiedRoleId);

                if (inPrimaryGuild)
                {
                    await guildUser.AddRoleAsync(primaryRole);
                }

                if (inSecondaryGuild)
                {
                    await guildUser.AddRoleAsync(secondaryRole);
                }

                await guildUser.AddRoleAsync(verifiedRole);

                // Edit message to send through the actual filled out message
                await command.ModifyOriginalResponseAsync(message => message.Content = output);
            }
            else
            {
                Console.WriteLine("[DON] API call failed");

                await command.ModifyOriginalResponseAsync(message => message.Content = $"Looks like you screwed up a couple of letters in the api key, try again mate, failed to process with API key: `{apiKey}`");
            }
        }

        private async Task DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            var accountFound = false;
            var output = "";

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var model = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                var account = model.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

                if (account != null)
                {
                    accountFound = true;
                    account.Gw2ApiKey = null;
                    context.Update(account);
                }

                await context.SaveChangesAsync();

                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)command.GuildId);

                output += accountFound ?
                    $"Deverify succeeded! Account data cleared for: `{command.User}`" :
                    $"Deverify unnecessary! No account data found for: `{command.User}`";

                if (guild == null)
                {
                    await command.ModifyOriginalResponseAsync(message => message.Content = output);
                    return;
                }
            }

            SocketGuildUser? guildUser;
            try
            {
                if (command.GuildId != null)
                {
                    guildUser = _client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
                }
                else
                {
                    throw new Exception("No GuildId");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing deverify nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to deverify, please try again.");
                return;
            }

            // Removes roles
            var primaryRoleId = guild.DiscordGuildMemberRoleId;
            var secondaryRoleId = guild.DiscordSecondaryMemberRoleId;
            var verifiedRoleId = guild.DiscordVerifiedRoleId;
            var user = (IGuildUser)guildUser;
            var primaryRole = ((IGuildChannel)command.Channel).Guild.GetRole((ulong)primaryRoleId);
            var secondaryRole = ((IGuildChannel)command.Channel).Guild.GetRole((ulong)secondaryRoleId);
            var verifiedRole = ((IGuildChannel)command.Channel).Guild.GetRole((ulong)verifiedRoleId);

            if (user.RoleIds.ToList().Contains((ulong)primaryRoleId))
            {
                await user.RemoveRoleAsync(primaryRole);
                output += $"\nRemoved `{primaryRole.Name}` role.";
            }

            if (user.RoleIds.ToList().Contains((ulong)secondaryRoleId))
            {
                await user.RemoveRoleAsync(secondaryRole);
                output += $"\nRemoved `{primaryRole.Name}` role.";
            }

            if (user.RoleIds.ToList().Contains((ulong)verifiedRoleId))
            {
                await user.RemoveRoleAsync(verifiedRole);
            }

            await command.ModifyOriginalResponseAsync(message => message.Content = output);
        }

        private async Task PointsCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            Account? account;
            int? rank = null;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var model = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                account = model.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);
                if (account != null)
                {
                    rank = model.OrderByDescending(o => o.Points).ToList().FindIndex(m => (ulong)m.DiscordId == command.User.Id) + 1;
                }
            }

            var output = "";
            output += account != null
                ? $"You have earned {Math.Round(account.Points)} points.{Environment.NewLine}You have {Math.Round(account.AvailablePoints)} Available Points for spending.{Environment.NewLine}Current Rank: {rank}"
                : "Unable to find account, have you verified?";


            await command.ModifyOriginalResponseAsync(message => message.Content = output);
        }

        private async Task PollingRolesTask(TimeSpan interval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await PollingRoles();
                await Task.Delay(interval, cancellationToken);
            }
        }

        private async Task PollingRoles()
        {
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var accounts = await context.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
                var guilds = await context.Guild.ToListAsync();
                var guildWars2Data = new Dictionary<long, GW2AccountDataModel?>();

                foreach (var account in accounts)
                {
                    Console.WriteLine($"=== FETCHING {account.Gw2AccountName.Trim()} ===");

                    var httpClient = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(20)
                    };

                    try
                    {
                        var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={account.Gw2ApiKey}");
                        if (response.IsSuccessStatusCode)
                        {
                            var stringData = await response.Content.ReadAsStringAsync();
                            var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                            guildWars2Data.Add(account.DiscordId, accountData);

                            account.FailedApiPullCount = 0;
                            account.World = Convert.ToInt32(accountData.World);
                        }
                        else
                        {
                            account.FailedApiPullCount = account.FailedApiPullCount == null ? 1 : account.FailedApiPullCount + 1;
                            guildWars2Data.Add(account.DiscordId, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        account.FailedApiPullCount = account.FailedApiPullCount == null ? 1 : account.FailedApiPullCount++;
                        Console.WriteLine($"=== FAILED Handling {account.Gw2AccountName.Trim()} ===");
                        Console.WriteLine(ex.Message);
                    }

                    context.Update(account);
                }

                await context.SaveChangesAsync();

                foreach (var clientGuild in _client.Guilds)
                {
                    var guild = clientGuild;
                    var guildUsers = guild.Users;

                    var guildConfiguration = guilds.FirstOrDefault(g => g.GuildId == (long)guild.Id);

                    if (guildConfiguration == null)
                    {
                        return;
                    }

                    var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
                    var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
                    var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

                    if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
                    {
                        continue;
                    }

                    // Role removal (strips roles from everybody in the guild)
                    try
                    {
                        var guildUserIds = guildUsers.Select(s => (long)s.Id);
                        var nonServerAccounts = accounts.Where(s => !guildUserIds.Contains(s.DiscordId)).ToList();
                        // TODO update db to have AccountGuild reference table and run this checking that table (i.e. don't add accounts that don't belong to that server
                        foreach (var nonServerAccount in nonServerAccounts)
                        {
                            nonServerAccount.Gw2ApiKey = null;
                            context.Update(nonServerAccount);

                            await context.SaveChangesAsync();
                        }

                        foreach (var user in guildUsers)
                        {
                            Console.WriteLine($"=== HANDLING {user.DisplayName} ===");
                            var primaryGuildId = guildConfiguration.Gw2GuildMemberRoleId;
                            var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds.Split(',');
                            var roles = user.Roles.Select(s => s.Id).ToList();

                            var account = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
                            if (account == null)
                            {
                                Console.WriteLine($"Failed to get {user.DisplayName} GW2 details, removing all roles.");

                                if (roles.Contains((ulong)primaryRoleId))
                                {
                                    await user.RemoveRoleAsync((ulong)primaryRoleId);
                                    Console.WriteLine(" - Removing Primary Role");
                                }

                                if (roles.Contains((ulong)secondaryRoleId))
                                {
                                    await user.RemoveRoleAsync((ulong)secondaryRoleId);
                                    Console.WriteLine(" - Removing Secondary Role");
                                }

                                if (roles.Contains((ulong)verifiedRoleId))
                                {
                                    await user.RemoveRoleAsync((ulong)verifiedRoleId);
                                    Console.WriteLine(" + Removing Verified Role");
                                }

                                continue;
                            }

                            if (account.FailedApiPullCount >= 48)
                            {
                                account.Gw2ApiKey = null;
                                context.Update(account);

                                await context.SaveChangesAsync();

                                var dmChannel = await user.CreateDMChannelAsync();
                                await dmChannel.SendMessageAsync($"Heyo {user.DisplayName}, I failed to pull your GW2 data using your API key throughout the day and have removed your roles for the server {guild.Name}, to get back the roles just use /verify in any channel in {guild.Name}.");

                                continue;
                            }

                            if (guildWars2Data.TryGetValue(account.DiscordId, out var accountData))
                            {
                                if (accountData == null)
                                {
                                    Console.WriteLine($"API KEY no longer valid for {user.DisplayName} GW2 details, removing all roles.");

                                    if (roles.Contains((ulong)primaryRoleId))
                                    {
                                        await user.RemoveRoleAsync((ulong)primaryRoleId);
                                        Console.WriteLine(" - Removing Primary Role");
                                    }

                                    if (roles.Contains((ulong)secondaryRoleId))
                                    {
                                        await user.RemoveRoleAsync((ulong)secondaryRoleId);
                                        Console.WriteLine(" - Removing Secondary Role");
                                    }

                                    if (roles.Contains((ulong)verifiedRoleId))
                                    {
                                        await user.RemoveRoleAsync((ulong)verifiedRoleId);
                                        Console.WriteLine(" + Removing Verified Role");
                                    }

                                    continue;
                                }
                                var inPrimaryGuild = accountData.Guilds.Contains(primaryGuildId);
                                var inSecondaryGuild = secondaryGuildIds.Any(guildId => accountData.Guilds.Contains(guildId));

                                if (roles.Contains((ulong)primaryRoleId) && !inPrimaryGuild)
                                {
                                    await user.RemoveRoleAsync((ulong)primaryRoleId);
                                    Console.WriteLine(" - Removing Primary Role");
                                }
                                else if (!roles.Contains((ulong)primaryRoleId) && inPrimaryGuild)
                                {
                                    await user.AddRoleAsync((ulong)primaryRoleId);
                                    Console.WriteLine(" + Adding Primary Role");
                                }

                                if (roles.Contains((ulong)secondaryRoleId) && !inSecondaryGuild)
                                {
                                    await user.RemoveRoleAsync((ulong)secondaryRoleId);
                                    Console.WriteLine(" - Removing Secondary Role");
                                }
                                else if (!roles.Contains((ulong)secondaryRoleId) && inSecondaryGuild)
                                {
                                    await user.AddRoleAsync((ulong)secondaryRoleId);
                                    Console.WriteLine(" + Adding Secondary Role");
                                }

                                if (!roles.Contains((ulong)verifiedRoleId))
                                {
                                    await user.AddRoleAsync((ulong)verifiedRoleId);
                                    Console.WriteLine(" + Adding Verified Role");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                    try
                    {
                        if (guildConfiguration.WvwPlayerActivityReportWebhook != null && _client.GetChannel((ulong)guildConfiguration.WvwPlayerActivityReportChannelId) is SocketTextChannel playerChannel)
                        {
                            var messages = await playerChannel.GetMessagesAsync(100).FlattenAsync();
                            await playerChannel.DeleteMessagesAsync(messages);

                            _messageGenerationService.GenerateWvWPlayerReport(guildConfiguration, clientGuild, guildConfiguration.WvwPlayerActivityReportWebhook);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private async Task DefaultCommandExecuted(SocketSlashCommand command)
        {
            await command.RespondAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            SocketGuild? guildUser;
            try
            {
                var channel = seenMessage.Channel as SocketGuildChannel;
                guildUser = channel?.Guild;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to parse user as socket guild user. Did not find user {seenMessage.Author.Username} in guild");
                return;
            }

            if (guildUser == null)
            {
                Console.WriteLine($"Did not find user {seenMessage.Author.Username} in guild");
                return;
            }

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildUser.Id);

                if (guild == null)
                {
                    return;
                }
            }

            // Ignore outside webhook + in upload channel + from Don
            if (seenMessage.Source != MessageSource.Webhook || 
                (seenMessage.Channel.Id != (ulong)guild.DebugWebhookChannelId && seenMessage.Channel.Id != (ulong)guild.WebhookChannelId) || 
                seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
            {
                return;
            }

            var webhookUrl = string.Empty;
            if (seenMessage.Channel.Id == (ulong)guild.DebugWebhookChannelId)
            {
                webhookUrl = guild.DebugWebhook;
            }
            else if (seenMessage.Channel.Id == (ulong)guild.WebhookChannelId)
            {
                webhookUrl = guild.Webhook;
            }

            if (!string.IsNullOrEmpty(webhookUrl))
            {
                var urls = seenMessage.Embeds.SelectMany((x => x.Fields.SelectMany(y => y.Value.Split('(')))).Where(x => x.Contains(")")).ToList();
                urls.AddRange(seenMessage.Embeds.Select(x => x.Url).Where(x => !string.IsNullOrEmpty(x)));

                var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

                foreach (var url in trimmedUrls)
                {
                    Console.WriteLine($"[DON] Assessing: {url}");
                    AnalyseAndReportOnUrl(url, guildUser.Id, guildUser, webhookUrl);
                }
            }
        }

        private async Task AnalyseAndReportOnUrl(string url, ulong guildId, SocketGuild socketGuild, string webhookUrl)
        {
            var seenUrls = _cacheService.Get<List<string>>(CacheKey.SeenUrls) ?? new List<string>();

            if (seenUrls.Contains(url))
            {
                Console.WriteLine($"[DON] Already seen, not analysing or reporting: {url}");
                return;
            }

            seenUrls.Add(url);
            _cacheService.Set(CacheKey.SeenUrls, seenUrls);

            Console.WriteLine($"[DON] Analysing and reporting on: {url}");
            var dataModelGenerator = new DataModelGenerationService();
            var data = await dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url);

            if (data.Success == false)
            {
                return;
            }

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var guilds = await context.Guild.ToListAsync();
                guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildId);

                if (guild == null)
                {
                    return;
                }
            }

            Console.WriteLine($"[DON] Generating fight summary: {url}");

            var webhook = new DiscordWebhookClient(webhookUrl);

            Embed message;
            if (data.Wvw)
            {
                var advancePlayerReportWebhook = new DiscordWebhookClient(guild.AdminAdvancePlayerReportWebhook);
                var advancedMessage = await _messageGenerationService.GenerateWvWFightSummary(data, true, false, guild);

                var playerReportWebhook = new DiscordWebhookClient(guild.AdminPlayerReportWebhook);
                var playerMessage = await _messageGenerationService.GenerateWvWPlayerSummary(socketGuild, guild);

                await advancePlayerReportWebhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { advancedMessage });

                if (guild.PlayerReportChannelId != null && _client.GetChannel((ulong)guild.PlayerReportChannelId) is SocketTextChannel playerChannel)
                {
                    var messages = await playerChannel.GetMessagesAsync(10).FlattenAsync();
                    await playerChannel.DeleteMessagesAsync(messages);
                    await playerReportWebhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { playerMessage });
                }
                message = await _messageGenerationService.GenerateWvWFightSummary(data, false, true, guild);
            }
            else
            {
                message = _messageGenerationService.GeneratePvEFightSummary(data);
            }

            await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            Console.WriteLine($"[DON] Completed and posted report on: {url}");
        }
    }
}
