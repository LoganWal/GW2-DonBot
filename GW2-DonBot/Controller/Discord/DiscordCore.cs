using System.Text.RegularExpressions;
using System.Threading;
using Discord;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Statics;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.LogGenerationServices;
using DonBot.Services.Logging;
using DonBot.Services.LogServices;
using DonBot.Services.PlayerServices;
using DonBot.Services.SecretsServices;
using DonBot.Services.WordleServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConnectionState = Discord.ConnectionState;

namespace DonBot.Controller.Discord
{
    public class DiscordCore : IDiscordCore
    {
        private readonly ILogger<DiscordCore> _logger;

        private readonly ISecretService _secretService;
        private readonly IMessageGenerationService _messageGenerationService;
        private readonly IVerifyCommandsService _verifyCommandsService;
        private readonly IPointsCommandsService _pointsCommandsService;
        private readonly IRaffleCommandsService _raffleCommandsService;
        private readonly IPollingTasksService _pollingTasksService;
        private readonly IPlayerService _playerService;
        private readonly IDataModelGenerationService _dataModelGenerator;
        private readonly IRaidService _raidService;
        private readonly IDiscordCommandService _discordCommandService;
        private readonly ILoggingService _loggingService;
        private readonly IFightLogService _fightLogService;
        private readonly ISteamCommandService _steamCommandService;
        private readonly IDeadlockCommandService _deadlockCommandService;

        private readonly SchedulerService _schedulerService;
        private readonly DatabaseContext _databaseContext;
        private readonly DiscordSocketClient _client;

        private readonly HashSet<string> _seenUrls = new();
        private const long DonBotId = 1021682849797111838;
        private CancellationTokenSource _pollingRolesCancellationTokenSource = new();
        private CancellationTokenSource _wordleBackgroundServiceCancellationTokenSource = new();

        public DiscordCore(
            ILogger<DiscordCore> logger,
            ISecretService secretService,
            IMessageGenerationService messageGenerationService,
            IVerifyCommandsService verifyCommandsService,
            IPointsCommandsService pointsCommandsService,
            IRaffleCommandsService raffleCommandsService,
            IPollingTasksService pollingTasksService,
            IPlayerService playerService,
            IRaidService raidService,
            IDataModelGenerationService dataModelGenerator,
            IDiscordCommandService discordCommandService,
            ILoggingService loggingService,
            IFightLogService fightLogService,
            ISteamCommandService steamCommandService,
            IDeadlockCommandService deadlockCommandService,
            SchedulerService schedulerService,
            DatabaseContext databaseContext,
            DiscordSocketClient client)
        {
            _logger = logger;
            _secretService = secretService;
            _messageGenerationService = messageGenerationService;
            _verifyCommandsService = verifyCommandsService;
            _pointsCommandsService = pointsCommandsService;
            _raffleCommandsService = raffleCommandsService;
            _pollingTasksService = pollingTasksService;
            _playerService = playerService;
            _raidService = raidService;
            _dataModelGenerator = dataModelGenerator;
            _discordCommandService = discordCommandService;
            _loggingService = loggingService;
            _fightLogService = fightLogService;
            _steamCommandService = steamCommandService;
            _deadlockCommandService = deadlockCommandService;
            _schedulerService = schedulerService;
            _databaseContext = databaseContext;
            _client = client;
        }

        public async Task MainAsync()
        {
            // Logging in...
            await _client.LoginAsync(TokenType.Bot, _secretService.FetchDonBotToken());
            await _client.StartAsync();

            _logger.LogInformation("GW2-DonBot attempting to connect...");

            // Wait for the client to be connected
            await WaitForConnectionAsync();
            await RegisterCommands(_client);

            _logger.LogInformation("GW2-DonBot connected.");

            // Load existing fight logs
            LoadExistingFightLogs();

            // Register event handlers
            RegisterEventHandlers();

            // Start polling roles task
            _pollingRolesCancellationTokenSource = new CancellationTokenSource();
            var pollingRolesTask = Task.Run(() => PollingRolesTask(TimeSpan.FromMinutes(30), _pollingRolesCancellationTokenSource.Token));

            _wordleBackgroundServiceCancellationTokenSource = new CancellationTokenSource();
            await _schedulerService.StartAsync(_wordleBackgroundServiceCancellationTokenSource.Token);
            
            _logger.LogInformation("GW2-DonBot setup - ready to cause chaos");

            // Block this task until the program is closed.
            var discordCoreCancellationToken = new CancellationToken();
            await Task.Delay(-1, discordCoreCancellationToken);

            // Safely close...
            UnregisterEventHandlers();

            _pollingRolesCancellationTokenSource.Cancel();
            await pollingRolesTask;

            _wordleBackgroundServiceCancellationTokenSource.Cancel();
        }

        private async Task WaitForConnectionAsync()
        {
            while (_client.ConnectionState != ConnectionState.Connected)
            {
                await Task.Delay(100);
            }
        }

        private void LoadExistingFightLogs()
        {
            var fightLogs = _databaseContext.FightLog.Select(s => s.Url).Distinct().ToList();
            foreach (var fightLog in fightLogs)
            {
                _seenUrls.Add(fightLog);
            }
        }

        private void RegisterEventHandlers()
        {
            _client.MessageReceived += MessageReceivedAsync;
            _client.Log += msg => _loggingService.LogAsync(msg);
            _client.SlashCommandExecuted += SlashCommandExecutedAsync;

            _client.ButtonExecuted += async buttonComponent =>
            {
                switch (buttonComponent.Data.CustomId)
                {
                    case ButtonId.Raffle1:
                        await _raffleCommandsService.HandleRaffleButton1(buttonComponent);
                        break;
                    case ButtonId.Raffle50:
                        await _raffleCommandsService.HandleRaffleButton50(buttonComponent);
                        break;
                    case ButtonId.Raffle100:
                        await _raffleCommandsService.HandleRaffleButton100(buttonComponent);
                        break;
                    case ButtonId.Raffle1000:
                        await _raffleCommandsService.HandleRaffleButton1000(buttonComponent);
                        break;
                    case ButtonId.RaffleRandom:
                        await _raffleCommandsService.HandleRaffleButtonRandom(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent1:
                        await _raffleCommandsService.HandleEventRaffleButton1(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent50:
                        await _raffleCommandsService.HandleEventRaffleButton50(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent100:
                        await _raffleCommandsService.HandleEventRaffleButton100(buttonComponent);
                        break;
                    case ButtonId.RaffleEvent1000:
                        await _raffleCommandsService.HandleEventRaffleButton1000(buttonComponent);
                        break;
                    case ButtonId.RaffleEventRandom:
                        await _raffleCommandsService.HandleEventRaffleButtonRandom(buttonComponent);
                        break;
                    case ButtonId.RafflePoints:
                        await _pointsCommandsService.PointsCommandExecuted(buttonComponent);
                        break;
                    case ButtonId.KnowMyEnemy:
                        await _fightLogService.GetEnemyInformation(buttonComponent);
                        break;
                    default:
                        break;
                }
            };
        }

        private void UnregisterEventHandlers()
        {
            _client.Log -= _loggingService.LogAsync;
            _client.MessageReceived -= MessageReceivedAsync;
            _client.SlashCommandExecuted -= SlashCommandExecutedAsync;
        }

        private async Task RegisterCommands(DiscordSocketClient client)
        {
            // Get all guilds
            var guilds = client.Guilds;

            // Clear existing global commands (if needed)
            await client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>());

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
                    _logger.LogInformation("Differences found in commands for {guildName}. Deleting all existing commands.", guild.Name);

                    // Delete all existing commands
                    await guild.DeleteApplicationCommandsAsync();

                    // Register all new commands
                    foreach (var command in newCommands)
                    {
                        _logger.LogInformation("Registering new command on {guildName} - {commandName}", guild.Name, command.Name);
                        await guild.CreateApplicationCommandAsync(command);
                    }
                }
                else
                {
                    _logger.LogInformation("No differences in commands for {guildName}. No action taken.", guild.Name);
                }
            }
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case        "gw2_verify":                   await Gw2VerifyCommandExecuted(command);                break;
                case        "gw2_deverify":                 await Gw2DeverifyCommandExecuted(command);              break;
                case        "gw2_points":                   await Gw2PointsCommandExecuted(command);                break;
                case        "gw2_create_raffle":            await Gw2CreateRaffleCommandExecuted(command);          break;
                case        "gw2_create_event_raffle":      await Gw2CreateEventRaffleCommandExecuted(command);     break;
                case        "gw2_enter_raffle":             await Gw2RaffleCommandExecuted(command);                break;
                case        "gw2_enter_event_raffle":       await Gw2EventRaffleCommandExecuted(command);           break;
                case        "gw2_complete_raffle":          await Gw2CompleteRaffleCommandExecuted(command);        break;
                case        "gw2_complete_event_raffle":    await Gw2CompleteEventRaffleCommandExecuted(command);   break;
                case        "gw2_reopen_raffle":            await Gw2ReopenRaffleCommandExecuted(command);          break;
                case        "gw2_reopen_event_raffle":      await Gw2ReopenEventRaffleCommandExecuted(command);     break;
                case        "gw2_start_raid":               await Gw2StartRaidCommandExecuted(command);             break;
                case        "gw2_close_raid":               await Gw2CloseRaidCommandExecuted(command);             break;
                case        "gw2_set_log_channel":          await Gw2SetLogChannel(command);                        break;
                case        "steam_verify":                 await SteamVerifyCommandExecuted(command);              break;
                case        "deadlock_mmr":                 await DeadlockMmrCommandExecuted(command);              break;
                case        "deadlock_mmr_history":         await DeadlockMmrHistoryCommandExecuted(command);       break;
                case        "deadlock_match_history":       await DeadlockMatchHistoryCommandExecuted(command);     break;
                default:                                    await DefaultCommandExecuted(command);                  break;
            }
        }

        private async Task Gw2StartRaidCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raidService.StartRaid(command, _client);
        }

        private async Task Gw2CloseRaidCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raidService.CloseRaid(command, _client);
        }

        private async Task Gw2ReopenRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.ReopenRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2ReopenEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.ReopenEventRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2CompleteRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.CompleteRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2CompleteEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.CompleteEventRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2RaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.RaffleCommandExecuted(command, _client);
        }

        private async Task Gw2EventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.EventRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2CreateRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.CreateRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2CreateEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _raffleCommandsService.CreateEventRaffleCommandExecuted(command, _client);
        }

        private async Task Gw2VerifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _verifyCommandsService.VerifyCommandExecuted(command, _client);
        }

        private async Task Gw2DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _verifyCommandsService.DeverifyCommandExecuted(command, _client);
        }

        private async Task Gw2PointsCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _pointsCommandsService.PointsCommandExecuted(command);
        }

        private async Task Gw2SetLogChannel(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _discordCommandService.SetLogChannel(command, _client);
        }

        private async Task SteamVerifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _steamCommandService.VerifySteamAccount(command, _client);
        }

        private async Task DeadlockMmrCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _deadlockCommandService.GetMmr(command, _client);
        }

        private async Task DeadlockMmrHistoryCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            await _deadlockCommandService.GetMmrHistory(command, _client);
        }

        private async Task DeadlockMatchHistoryCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral:true);
            await _deadlockCommandService.GetMatchHistory(command, _client);
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
                    await _pollingTasksService.PollingRoles(_client);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while polling roles");
                }
                await Task.Delay(interval, cancellationToken);
            }
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            await HandleMessage(seenMessage);
        }

        private async Task HandleMessage(SocketMessage seenMessage)
        {
            try
            {
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

                if (seenMessage.Channel is not SocketGuildChannel channel)
                {
                    _logger.LogWarning("Did not find channel {seenMessageChannelName} in guild", seenMessage.Channel.Name);
                    return;
                }

                var guild = _databaseContext.Guild.FirstOrDefault(g => g.GuildId == (long)channel.Guild.Id);
                if (guild == null || !guild.LogDropOffChannelId.HasValue)
                {
                    _logger.LogWarning("Unable to find guild {channelGuildId}", channel.Guild.Id);
                    return;
                }


                if (guild.RemoveSpamEnabled && Regex.IsMatch(seenMessage.Content, @"\b((https?|ftp)://|www\.|(\w+\.)+\w{2,})(\S*)\b"))
                {
                    if (seenMessage.Channel is not SocketTextChannel messageChannel)
                    {
                        _logger.LogWarning("Unable to spam channel {seenMessageChannelName}", seenMessage.Channel.Name);
                        return;
                    }

                    var user = _databaseContext.Account.FirstOrDefault(g => g.DiscordId == (long)seenMessage.Author.Id);
                    if (user == null)
                    {
                        HandleSpamMessage(seenMessage, messageChannel);
                        return;
                    }

                    if (guild.DiscordVerifiedRoleId != null)
                    {
                        if (seenMessage.Author is SocketGuildUser socketUser && !socketUser.Roles.Select(s => (long)s.Id).ToList().Contains(guild.DiscordVerifiedRoleId.Value))
                        {
                            HandleSpamMessage(seenMessage, messageChannel);
                            return;
                        }
                    }
                }

                bool embedMessage;
                List<string> trimmedUrls;
                if (seenMessage.Channel is not SocketTextChannel replyChannel)
                {
                    _logger.LogWarning("Unable to find channel {seenMessageChannelName}", seenMessage.Channel.Name);
                    return;
                }

                if (seenMessage.Source != MessageSource.Webhook || seenMessage.Channel.Id != (ulong)guild.LogDropOffChannelId)
                {
                    embedMessage = false;

                    const string pattern = @"https://(?:wvw|dps)\.report/\S+";
                    var matches = Regex.Matches(seenMessage.Content, pattern);

                    trimmedUrls = matches.Select(match => match.Value).ToList();

                    const string wingmanPattern = @"https://gw2wingman\.nevermindcreations\.de/log/\S+";
                    matches = Regex.Matches(seenMessage.Content, wingmanPattern);

                    var wingmanMatches = matches.Cast<Match>().Select(match => match.Value).ToList();
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
                    foreach (var url in trimmedUrls)
                    {
                        _logger.LogInformation("Assessing: {url}", url);
                        await AnalyseAndReportOnUrl(url, channel.Guild.Id, embedMessage, replyChannel);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to handle message");
            }
        }

        private void HandleSpamMessage(SocketMessage seenMessage, SocketTextChannel messageChannel)
        {
            seenMessage.DeleteAsync();

            var discordGuild = (seenMessage.Channel as SocketGuildChannel)?.Guild;
            if (discordGuild != null)
            {
                var guildId = discordGuild.Id;

                var guild = _databaseContext.Guild.FirstOrDefault(g => g.GuildId == (long)guildId);
                if (guild == null || !guild.RemovedMessageChannelId.HasValue)
                {
                    _logger.LogWarning("Unable to find guild {guildId}", guildId);
                    return;
                }

                if (_client.GetChannel((ulong)guild.RemovedMessageChannelId) is not ITextChannel targetChannel)
                {
                    _logger.LogWarning("Unable to find guild remove channel {guildRemovedMessageChannelId}", guild.RemovedMessageChannelId);
                    return;
                }

                targetChannel.SendMessageAsync($"Removed message from <@{seenMessage.Author.Id}> ({seenMessage.Author.Username}), for posting a discord link without being verified.");
            }
        }

        private async Task AnalyseAndReportOnUrl(string url, ulong guildId, bool isEmbed, SocketTextChannel replyChannel)
        {
            if (isEmbed && _seenUrls.Contains(url))
            {
                _logger.LogWarning("Already seen, not analysing or reporting: {url}", url);
                return;
            }

            _seenUrls.Add(url);

            _logger.LogInformation("Analysing and reporting on: {url}", url);
            var data = await _dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url);

            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildId);

            if (guild == null)
            {
                return;
            }

            _logger.LogInformation("Generating fight summary: {url}", url);

            if (guild.LogReportChannelId == null)
            {
                _logger.LogWarning("no log report channel id for guild id `{guildId}`", guild.GuildId);
                return;
            }

            if (_client.GetChannel((ulong)guild.LogReportChannelId) is not ITextChannel logReportChannel)
            {
                _logger.LogWarning("Failed to find the target channel {guildLogReportChannelId}", guild.LogReportChannelId);
                return;
            }

            Embed message;
            MessageComponent? buttonBuilder = null;

            if (data.Wvw)
            {
                if (isEmbed)
                {
                    if (guild.AdvanceLogReportChannelId != null)
                    {
                        if (_client.GetChannel((ulong)guild.AdvanceLogReportChannelId) is not ITextChannel advanceLogReportChannel)
                        {
                            _logger.LogWarning("Failed to find the target channel {guildAdvanceLogReportChannelId}", guild.AdvanceLogReportChannelId);
                            return;
                        }

                        var advancedMessage = _messageGenerationService.GenerateWvWFightSummary(data, true, guild, _client);
                        await advanceLogReportChannel.SendMessageAsync(text: "", embeds: new[] { advancedMessage });
                    }

                    if (guild.PlayerReportChannelId != null && _client.GetChannel((ulong)guild.PlayerReportChannelId) is SocketTextChannel playerChannel)
                    {
                        var messages = await playerChannel.GetMessagesAsync(10).FlattenAsync();
                        await playerChannel.DeleteMessagesAsync(messages);

                        var activePlayerMessage = await _messageGenerationService.GenerateWvWActivePlayerSummary(guild, url);
                        var playerMessage = await _messageGenerationService.GenerateWvWPlayerSummary(guild);

                        await playerChannel.SendMessageAsync(text: "", embeds: new[] { activePlayerMessage });
                        await playerChannel.SendMessageAsync(text: "", embeds: new[] { playerMessage });
                    }

                    await _playerService.SetPlayerPoints(data);
                }

                message = _messageGenerationService.GenerateWvWFightSummary(data, false, guild, _client);

                buttonBuilder = new ComponentBuilder()
                    .WithButton("Know My Enemy", ButtonId.KnowMyEnemy, ButtonStyle.Primary)
                    .Build();
            }
            else
            {
                message = _messageGenerationService.GeneratePvEFightSummary(data, (long)guildId);
            }

            if (isEmbed)
            {
                await logReportChannel.SendMessageAsync(text: "", embeds: new[] { message }, components: buttonBuilder);
            }
            else
            {
                //TODO: update non embed to give option to user if they want to show the message, if so show, if many show aggregate
                //await replyChannel.SendMessageAsync(text: "", embeds: new[] { message }, components: buttonBuilder);
            }
            _logger.LogInformation("Completed and posted report on: {url}", url);
        }
    }
}
