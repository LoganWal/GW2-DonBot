using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.GuildWars2;
using DonBot.Models.Statics;
using DonBot.Services.DatabaseServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.LoggingServices;
using DonBot.Services.SecretsServices;
using DonBot.Services.WordleServices;
using Microsoft.Extensions.Logging;
using static System.Text.RegularExpressions.Regex;
using ConnectionState = Discord.ConnectionState;

namespace DonBot.Controller.Discord
{
    public class DiscordCore(
        IEntityService entityService,
        ILogger<DiscordCore> logger,
        ISecretService secretService,
        IMessageGenerationService messageGenerationService,
        IVerifyCommandsService verifyCommandsService,
        IPointsCommandsService pointsCommandsService,
        IRaffleCommandsService raffleCommandsService,
        IPollingTasksService pollingTasksService,
        IPlayerService playerService,
        IRaidCommandService raidCommandService,
        IDataModelGenerationService dataModelGenerator,
        IDiscordCommandService discordCommandService,
        ILoggingService loggingService,
        IFightLogService fightLogService,
        ISteamCommandService steamCommandService,
        IDeadlockCommandService deadlockCommandService,
        SchedulerService schedulerService,
        DiscordSocketClient client)
        : IDiscordCore
    {
        private readonly HashSet<string> _seenUrls = [];
        private const long DonBotId = 1021682849797111838;
        private CancellationTokenSource _pollingRolesCancellationTokenSource = new();
        private CancellationTokenSource _wordleBackgroundServiceCancellationTokenSource = new();

        public async Task MainAsync()
        {
            // Logging in...
            await client.LoginAsync(TokenType.Bot, secretService.FetchDonBotToken());
            await client.StartAsync();

            logger.LogInformation("GW2-DonBot attempting to connect...");

            // Wait for the client to be connected
            await WaitForConnectionAsync();
            await RegisterCommands();

            logger.LogInformation("GW2-DonBot connected.");

            // Load existing fight logs
            await LoadExistingFightLogs();

            // Register event handlers
            RegisterEventHandlers();

            // Start polling roles task
            _pollingRolesCancellationTokenSource = new CancellationTokenSource();
            var pollingRolesTask = Task.Run(() => PollingRolesTask(TimeSpan.FromMinutes(30), _pollingRolesCancellationTokenSource.Token));

            _wordleBackgroundServiceCancellationTokenSource = new CancellationTokenSource();
            await schedulerService.StartAsync(_wordleBackgroundServiceCancellationTokenSource.Token);
            
            logger.LogInformation("GW2-DonBot setup - ready to cause chaos");

            // Block this task until the program is closed.
            var discordCoreCancellationToken = CancellationToken.None;
            await Task.Delay(-1, discordCoreCancellationToken);

            // Safely close...
            UnregisterEventHandlers();

            await _pollingRolesCancellationTokenSource.CancelAsync();
            await pollingRolesTask;

            await _wordleBackgroundServiceCancellationTokenSource.CancelAsync();
        }

        private async Task WaitForConnectionAsync()
        {
            while (client.ConnectionState != ConnectionState.Connected)
            {
                await Task.Delay(100);
            }
        }

        private async Task LoadExistingFightLogs()
        {
            var fightLogs = (await entityService.FightLog.GetAllAsync()).Select(s => s.Url).Distinct().ToList();
            foreach (var fightLog in fightLogs)
            {
                _seenUrls.Add(fightLog);
            }
        }

        private void RegisterEventHandlers()
        {
            client.MessageReceived += MessageReceivedAsync;
            client.Log += loggingService.LogAsync;
            client.SlashCommandExecuted += SlashCommandExecutedAsync;

            client.ButtonExecuted += async buttonComponent =>
            {
                switch (buttonComponent.Data.CustomId)
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
                }
            };
        }

        private void UnregisterEventHandlers()
        {
            client.Log -= loggingService.LogAsync;
            client.MessageReceived -= MessageReceivedAsync;
            client.SlashCommandExecuted -= SlashCommandExecutedAsync;
        }

        private async Task RegisterCommands()
        {
            // Get all guilds
            var guilds = client.Guilds;

            // Clear existing global commands (if needed)
            await client.BulkOverwriteGlobalApplicationCommandsAsync([]);

            // Define commands to register
            var commandsToRegister = new List<SlashCommandBuilder>
            {
                new SlashCommandBuilder()
                    .WithName("gw2_verify")
                    .WithDescription("Verify your GW2 API key so that your GW2 Account and Discord are linked.")
                    .AddOption("api-key", ApplicationCommandOptionType.String, "The API key you wish to link",
                        isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_deverify")
                    .WithDescription("Remove any /verify information stored for your Discord account."),

                new SlashCommandBuilder()
                    .WithName("gw2_points")
                    .WithDescription("Check how many points you have earned."),

                new SlashCommandBuilder()
                    .WithName("gw2_create_raffle")
                    .WithDescription("Create a raffle.")
                    .AddOption("raffle-description", ApplicationCommandOptionType.String, "The Raffle description",
                        isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_create_event_raffle")
                    .WithDescription("Create an event raffle.")
                    .AddOption("raffle-description", ApplicationCommandOptionType.String, "The Raffle description",
                        isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_enter_raffle")
                    .WithDescription("RAFFLE TIME.")
                    .AddOption("points-to-spend", ApplicationCommandOptionType.Integer,
                        "How many points do you want to spend?", isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_enter_event_raffle")
                    .WithDescription("RAFFLE TIME.")
                    .AddOption("points-to-spend", ApplicationCommandOptionType.Integer,
                        "How many points do you want to spend?", isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_complete_raffle")
                    .WithDescription("Complete the raffle."),

                new SlashCommandBuilder()
                    .WithName("gw2_complete_event_raffle")
                    .WithDescription("Complete the event raffle.")
                    .AddOption("how-many-winners", ApplicationCommandOptionType.Integer,
                        "How many winners for the event raffle?", isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_reopen_raffle")
                    .WithDescription("Reopen the raffle."),

                new SlashCommandBuilder()
                    .WithName("gw2_reopen_event_raffle")
                    .WithDescription("Reopen the event raffle."),

                new SlashCommandBuilder()
                    .WithName("gw2_start_raid")
                    .WithDescription("Starts raid."),

                new SlashCommandBuilder()
                    .WithName("gw2_close_raid")
                    .WithDescription("Closes raid."),

                new SlashCommandBuilder()
                    .WithName("gw2_start_alliance_raid")
                    .WithDescription("Starts alliance raid.")
                    .AddOption("raid-message", ApplicationCommandOptionType.String, "Message in your raid alert, feel free to link your discord join link",
                        isRequired: true),

                new SlashCommandBuilder()
                    .WithName("gw2_set_log_channel")
                    .WithDescription("Set the channel for simple logs.")
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "Which channel?", isRequired: true),

                new SlashCommandBuilder()
                    .WithName("steam_verify")
                    .WithDescription("verify steam account.")
                    .AddOption("steam-id", ApplicationCommandOptionType.String,
                        "Steam account id shown on your account page", isRequired: true),

                new SlashCommandBuilder()
                    .WithName("deadlock_mmr")
                    .WithDescription("Get your deadlock mmr."),

                new SlashCommandBuilder()
                    .WithName("deadlock_mmr_history")
                    .WithDescription("Get your deadlock mmr history."),

                new SlashCommandBuilder()
                    .WithName("deadlock_match_history")
                    .WithDescription("Get your deadlock match history."),
            };

            // Build the commands for comparison
            var newCommands = commandsToRegister.Select(c => c.Build()).ToList();

            foreach (var guild in guilds)
            {
                // Get existing commands for the guild
                var existingCommands = await guild.GetApplicationCommandsAsync();

                // Check if there are any differences
                if (existingCommands.Count != newCommands.Count || existingCommands.Any(ec => newCommands.All(nc => nc.Name.Value != ec.Name)))
                {
                    // Log the deletion of existing commands
                    logger.LogInformation("Differences found in commands for {guildName}. Deleting all existing commands.", guild.Name);

                    // Delete all existing commands
                    await guild.DeleteApplicationCommandsAsync();

                    // Register all new commands
                    foreach (var command in newCommands)
                    {
                        logger.LogInformation("Registering new command on {guildName} - {commandName}", guild.Name, command.Name);
                        await guild.CreateApplicationCommandAsync(command);
                    }
                }
                else
                {
                    logger.LogInformation("No differences in commands for {guildName}. No action taken.", guild.Name);
                }
            }
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            try
            {
                switch (command.Data.Name)
                {
                    case "gw2_verify": await Gw2VerifyCommandExecuted(command); break;
                    case "gw2_deverify": await Gw2DeverifyCommandExecuted(command); break;
                    case "gw2_points": await Gw2PointsCommandExecuted(command); break;
                    case "gw2_create_raffle": await Gw2CreateRaffleCommandExecuted(command); break;
                    case "gw2_create_event_raffle": await Gw2CreateEventRaffleCommandExecuted(command); break;
                    case "gw2_enter_raffle": await Gw2RaffleCommandExecuted(command); break;
                    case "gw2_enter_event_raffle": await Gw2EventRaffleCommandExecuted(command); break;
                    case "gw2_complete_raffle": await Gw2CompleteRaffleCommandExecuted(command); break;
                    case "gw2_complete_event_raffle": await Gw2CompleteEventRaffleCommandExecuted(command); break;
                    case "gw2_reopen_raffle": await Gw2ReopenRaffleCommandExecuted(command); break;
                    case "gw2_reopen_event_raffle": await Gw2ReopenEventRaffleCommandExecuted(command); break;
                    case "gw2_start_raid": await Gw2StartRaidCommandExecuted(command); break;
                    case "gw2_close_raid": await Gw2CloseRaidCommandExecuted(command); break;
                    case "gw2_start_alliance_raid": await Gw2StartAllianceRaidCommandExecuted(command); break;
                    case "gw2_set_log_channel": await Gw2SetLogChannel(command); break;
                    case "steam_verify": await SteamVerifyCommandExecuted(command); break;
                    case "deadlock_mmr": await DeadlockMmrCommandExecuted(command); break;
                    case "deadlock_mmr_history": await DeadlockMmrHistoryCommandExecuted(command); break;
                    case "deadlock_match_history": await DeadlockMatchHistoryCommandExecuted(command); break;
                    default: await DefaultCommandExecuted(command); break;
                }
            }
            catch (TimeoutException ex)
            {
                logger.LogError(ex, "Timeout occurred on {commandDataName} command", command.Data.Name);
                await command.ModifyOriginalResponseAsync(msg => msg.Content = "Request timeout - Yell at Logan");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed {commandDataName} command", command.Data.Name);
            }
        }

        private async Task Gw2StartRaidCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raidCommandService.StartRaid(command, client);
        }

        private async Task Gw2CloseRaidCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raidCommandService.CloseRaid(command, client);
        }

        private async Task Gw2StartAllianceRaidCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raidCommandService.StartAllianceRaid(command, client);
        }

        private async Task Gw2ReopenRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.ReopenRaffleCommandExecuted(command, client);
        }

        private async Task Gw2ReopenEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.ReopenEventRaffleCommandExecuted(command, client);
        }

        private async Task Gw2CompleteRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.CompleteRaffleCommandExecuted(command, client);
        }

        private async Task Gw2CompleteEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.CompleteEventRaffleCommandExecuted(command, client);
        }

        private async Task Gw2RaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.RaffleCommandExecuted(command, client);
        }

        private async Task Gw2EventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.EventRaffleCommandExecuted(command, client);
        }

        private async Task Gw2CreateRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.CreateRaffleCommandExecuted(command, client);
        }

        private async Task Gw2CreateEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await raffleCommandsService.CreateEventRaffleCommandExecuted(command, client);
        }

        private async Task Gw2VerifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await verifyCommandsService.VerifyCommandExecuted(command, client);
        }

        private async Task Gw2DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await verifyCommandsService.DeverifyCommandExecuted(command, client);
        }

        private async Task Gw2PointsCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await pointsCommandsService.PointsCommandExecuted(command);
        }

        private async Task Gw2SetLogChannel(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await discordCommandService.SetLogChannel(command, client);
        }

        private async Task SteamVerifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await steamCommandService.VerifySteamAccount(command, client);
        }

        private async Task DeadlockMmrCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await deadlockCommandService.GetMmr(command, client);
        }

        private async Task DeadlockMmrHistoryCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await deadlockCommandService.GetMmrHistory(command, client);
        }

        private async Task DeadlockMatchHistoryCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral:true);
            await deadlockCommandService.GetMatchHistory(command, client);
        }

        private static async Task DefaultCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await command.FollowupAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
        }

        private async Task PollingRolesTask(TimeSpan interval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await pollingTasksService.PollingRoles(client);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred while polling roles");
                }
                await Task.Delay(interval, cancellationToken);
            }
        }

        private Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            _ = HandleMessage(seenMessage);
            return Task.CompletedTask;
        }

        private async Task HandleMessage(SocketMessage seenMessage)
        {
            try
            {
                // TODO update this to be config driven
                var knownBots = new List<ulong>
                {
                    DonBotId,
                    1172050606005964820 // gw2Mists.com
                };

                var isKnownBot = knownBots.Contains(seenMessage.Author.Id);

                if (isKnownBot)
                {
                    return;
                }

                Guild? guild;
                if (seenMessage.Channel is not SocketGuildChannel channel)
                {
                    logger.LogWarning("Did not find channel {seenMessageChannelName} in guild", seenMessage.Channel.Name);
                    guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == -1);
                }
                else
                {
                    guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)channel.Guild.Id);
                }

                if ((guild?.RemoveSpamEnabled ?? false) && IsMatch(seenMessage.Content, @"\b((https?|ftp)://|www\.|(\w+\.)+\w{2,})(\S*)\b"))
                {
                    if (seenMessage.Channel is not SocketTextChannel)
                    {
                        logger.LogWarning("Unable to spam channel {seenMessageChannelName}", seenMessage.Channel.Name);
                        return;
                    }

                    var user = await entityService.Account.GetFirstOrDefaultAsync(g => g.DiscordId == (long)seenMessage.Author.Id);
                    if (user == null)
                    {
                        await HandleSpamMessage(seenMessage);
                        return;
                    }

                    if (guild.DiscordVerifiedRoleId != null)
                    {
                        if (seenMessage.Author is SocketGuildUser socketUser && !socketUser.Roles.Select(s => (long)s.Id).ToList().Contains(guild.DiscordVerifiedRoleId.Value))
                        {
                            await HandleSpamMessage(seenMessage);
                            return;
                        }
                    }
                }

                bool embedMessage;
                List<string> trimmedUrls;
                if (seenMessage.Source != MessageSource.Webhook || seenMessage.Channel.Id != (ulong)(guild?.LogDropOffChannelId ?? -1))
                {
                    embedMessage = false;

                    const string pattern = @"https://(?:wvw|dps)\.report/\S+";
                    var matches = Matches(seenMessage.Content, pattern);

                    trimmedUrls = matches.Select(match => match.Value).ToList();

                    const string wingmanPattern = @"https://gw2wingman\.nevermindcreations\.de/log/\S+";
                    matches = Matches(seenMessage.Content, wingmanPattern);

                    var wingmanMatches = matches.Select(match => match.Value).ToList();
                    for (var i = 0; i < wingmanMatches.Count; i++)
                    {
                        wingmanMatches[i] = wingmanMatches[i].Replace("https://gw2wingman.nevermindcreations.de/log/", "https://dps.report/");
                    }

                    trimmedUrls.AddRange(wingmanMatches);
                }
                else
                {
                    embedMessage = true;

                    var urls = seenMessage.Embeds.SelectMany(x => x.Fields.SelectMany(y => y.Value.Split('('))).Where(x => x.Contains(")")).ToList();
                    urls.AddRange(seenMessage.Embeds.Select(x => x.Url).Where(x => !string.IsNullOrEmpty(x)));

                    trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();
                }

                if (trimmedUrls.Any())
                {
                    logger.LogInformation("Assessing: {url}", string.Join(",", trimmedUrls));
                    await AnalyseAndReportOnUrl(trimmedUrls, guild?.GuildId ?? -1, embedMessage, seenMessage.Channel);
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to handle message");
            }
        }

        private async Task HandleSpamMessage(SocketMessage seenMessage)
        {
            await seenMessage.DeleteAsync();

            var discordGuild = (seenMessage.Channel as SocketGuildChannel)?.Guild;
            if (discordGuild != null)
            {
                var guildId = discordGuild.Id;

                var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)guildId);
                if (guild?.RemovedMessageChannelId == null)
                {
                    logger.LogWarning("Unable to find guild {guildId}", guildId);
                    return;
                }

                if (client.GetChannel((ulong)guild.RemovedMessageChannelId) is not ITextChannel targetChannel)
                {
                    logger.LogWarning("Unable to find guild remove channel {guildRemovedMessageChannelId}", guild.RemovedMessageChannelId);
                    return;
                }

                await targetChannel.SendMessageAsync($"Removed message from <@{seenMessage.Author.Id}> ({seenMessage.Author.Username}), for posting a discord link without being verified.");
            }
        }

        private async Task AnalyseAndReportOnUrl(List<string> urls, long guildId, bool isEmbed, ISocketMessageChannel replyChannel)
        {
            var urlList = string.Join(",", urls);
            if (isEmbed && urls.All(url => _seenUrls.Contains(url)))
            {
                logger.LogWarning("Already seen, not analysing or reporting: {url}", urlList);
                return;
            }

            logger.LogInformation("Analysing and reporting on: {url}", urlList);
            var dataList = new List<EliteInsightDataModel>();

            foreach (var url in urls)
            {
                dataList.Add(await dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url));
            }

            var guilds = await entityService.Guild.GetAllAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == guildId) ?? guilds.Single(s => s.GuildId == -1);

            logger.LogInformation("Generating fight summary: {url}", urlList);

            Embed message;
            MessageComponent? buttonBuilder = null;
            if (isEmbed)
            {
                foreach (var eliteInsightDataModel in dataList)
                {
                    if (_seenUrls.Contains(eliteInsightDataModel.Url))
                    {
                        logger.LogInformation("Already seen {url}, going to next log.", eliteInsightDataModel.Url);
                        continue;
                    }
                    if (eliteInsightDataModel.Wvw)
                    {
                        if (guild.AdvanceLogReportChannelId != null)
                        {
                            if (client.GetChannel((ulong)guild.AdvanceLogReportChannelId) is not ITextChannel
                                advanceLogReportChannel)
                            {
                                logger.LogWarning("Failed to find the target channel {guildAdvanceLogReportChannelId}", guild.AdvanceLogReportChannelId);
                                await replyChannel.SendMessageAsync("Failed to find the advanced log report channel.");
                                continue;
                            }

                            var advancedMessage = await messageGenerationService.GenerateWvWFightSummary(eliteInsightDataModel, true, guild, client);
                            await advanceLogReportChannel.SendMessageAsync(text: "", embeds: [advancedMessage]);
                        }

                        if (guild.PlayerReportChannelId != null && client.GetChannel((ulong)guild.PlayerReportChannelId) is SocketTextChannel playerChannel)
                        {
                            var messages = await playerChannel.GetMessagesAsync(10).FlattenAsync();
                            await playerChannel.DeleteMessagesAsync(messages);

                            var activePlayerMessage = await messageGenerationService.GenerateWvWActivePlayerSummary(guild, eliteInsightDataModel.Url);
                            var playerMessage = await messageGenerationService.GenerateWvWPlayerSummary(guild);

                            await playerChannel.SendMessageAsync(text: "", embeds: [activePlayerMessage]);
                            await playerChannel.SendMessageAsync(text: "", embeds: [playerMessage]);
                        }

                        await playerService.SetPlayerPoints(eliteInsightDataModel);

                        message = await messageGenerationService.GenerateWvWFightSummary(eliteInsightDataModel, false, guild, client);
                        buttonBuilder = new ComponentBuilder()
                            .WithButton("Know My Enemy", ButtonId.KnowMyEnemy)
                            .Build();
                    }
                    else
                    {
                        message = await messageGenerationService.GeneratePvEFightSummary(eliteInsightDataModel, (long)guildId);
                    }

                    if (guild.LogReportChannelId == null)
                    {
                        logger.LogWarning("no log report channel id for guild id `{guildId}`", guild.GuildId);
                        return;
                    }

                    if (client.GetChannel((ulong)guild.LogReportChannelId) is not ITextChannel logReportChannel)
                    {
                        logger.LogWarning("Failed to find the target channel {guildLogReportChannelId}", guild.LogReportChannelId);
                        return;
                    }

                    await logReportChannel.SendMessageAsync(text: "", embeds: [message], components: buttonBuilder);
                }
            }
            else
            {
                if (dataList.Count > 1)
                {
                    foreach (var eliteInsightDataModel in dataList)
                    {
                        if (eliteInsightDataModel.Wvw)
                        {
                            await messageGenerationService.GenerateWvWFightSummary(eliteInsightDataModel, false, guild, client);
                        }
                        else
                        {
                            await messageGenerationService.GeneratePvEFightSummary(eliteInsightDataModel, guildId);
                        }
                    }

                    var messages = await messageGenerationService.GenerateRaidReplyReport(urls);
                    if (messages != null)
                    {
                        foreach (var bulkMessage in messages)
                        {
                            await replyChannel.SendMessageAsync(embeds: [bulkMessage]);
                        }
                    }
                }
                else
                {
                    if (dataList.First().Wvw)
                    {
                        message = await messageGenerationService.GenerateWvWFightSummary(dataList.First(), false, guild, client);
                    }
                    else
                    {
                        message = await messageGenerationService.GeneratePvEFightSummary(dataList.First(), guild.GuildId);
                    }
                    await replyChannel.SendMessageAsync(text: "", embeds: [message], components: buttonBuilder);
                }
            }

            logger.LogInformation("Completed and posted report on: {url}", urlList);
            foreach (var url in urls)
            {
                _seenUrls.Add(url);
            }
        }

    }
}
