using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.Statics;
using Services.DiscordRequestServices;
using Services.LogGenerationServices;
using Services.Logging;
using Services.PlayerServices;
using Services.SecretsServices;
using System.Text.RegularExpressions;
using ConnectionState = Discord.ConnectionState;

namespace Controller.Discord
{
    public class DiscordCore : IDiscordCore
    {
        private readonly ILogger<DiscordCore> _logger;

        private readonly ISecretService _secretService;
        private readonly IMessageGenerationService _messageGenerationService;
        private readonly IGenericCommandsService _genericCommandsService;
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

        private readonly DatabaseContext _databaseContext;
        private readonly DiscordSocketClient _client;

        private readonly HashSet<string> _seenUrls = new();
        private const long DonBotId = 1021682849797111838;
        private CancellationTokenSource _pollingRolesCancellationTokenSource = new();

        public DiscordCore(
            ILogger<DiscordCore> logger,
            ISecretService secretService,
            IMessageGenerationService messageGenerationService,
            IGenericCommandsService genericCommandsService,
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
            DatabaseContext databaseContext,
            DiscordSocketClient client)
        {
            _logger = logger;
            _secretService = secretService;
            _messageGenerationService = messageGenerationService;
            _genericCommandsService = genericCommandsService;
            _verifyCommandsService = verifyCommandsService;
            _pointsCommandsService = pointsCommandsService;
            _raffleCommandsService = raffleCommandsService;
            _pollingTasksService = pollingTasksService;
            _playerService = playerService;
            _raidService = raidService;
            _dataModelGenerator = dataModelGenerator;
            _discordCommandService = discordCommandService;
            _databaseContext = databaseContext;
            _loggingService = loggingService;
            _fightLogService = fightLogService;

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

            _logger.LogInformation("GW2-DonBot connected.");

            // Load existing fight logs
            LoadExistingFightLogs();

            // Register event handlers
            RegisterEventHandlers();

            // Start polling roles task
            _pollingRolesCancellationTokenSource = new CancellationTokenSource();
            var pollingRolesTask = PollingRolesTask(TimeSpan.FromMinutes(30), _pollingRolesCancellationTokenSource.Token);

            _logger.LogInformation("GW2-DonBot setup - ready to cause chaos");

            // Block this task until the program is closed.
            await Task.Delay(-1, _pollingRolesCancellationTokenSource.Token);

            // Safely close...
            UnregisterEventHandlers();
            _pollingRolesCancellationTokenSource.Cancel();
            await pollingRolesTask;
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

                var startRaid = new SlashCommandBuilder()
                    .WithName("start_raid")
                    .WithDescription("Starts raid.");

                await guild.CreateApplicationCommandAsync(startRaid.Build());

                var closeRaid = new SlashCommandBuilder()
                    .WithName("close_raid")
                    .WithDescription("Closes raid.");

                await guild.CreateApplicationCommandAsync(closeRaid.Build());

                var setLogChannel = new SlashCommandBuilder()
                    .WithName("set_log_channel")
                    .WithDescription("Set the channel for simple logs.")
                    .AddOption("channel", ApplicationCommandOptionType.Channel,
                        "Which channel?", isRequired: true);

                await guild.CreateApplicationCommandAsync(setLogChannel.Build());
            }
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case        "help":                     await HelpCommandExecuted(command);                  break;
                case        "verify":                   await VerifyCommandExecuted(command);                break;
                case        "deverify":                 await DeverifyCommandExecuted(command);              break;
                case        "points":                   await PointsCommandExecuted(command);                break;
                case        "create_raffle":            await CreateRaffleCommandExecuted(command);          break;
                case        "create_event_raffle":      await CreateEventRaffleCommandExecuted(command);     break;
                case        "enter_raffle":             await RaffleCommandExecuted(command);                break;
                case        "enter_event_raffle":       await EventRaffleCommandExecuted(command);           break;
                case        "complete_raffle":          await CompleteRaffleCommandExecuted(command);        break;
                case        "complete_event_raffle":    await CompleteEventRaffleCommandExecuted(command);   break;
                case        "reopen_raffle":            await ReopenRaffleCommandExecuted(command);          break;
                case        "reopen_event_raffle":      await ReopenEventRaffleCommandExecuted(command);     break;
                case        "start_raid":               await StartRaidCommandExecuted(command);             break;
                case        "close_raid":               await CloseRaidCommandExecuted(command);             break;
                case        "set_log_channel":          await SetLogChannel(command);                        break;
                default:                                await DefaultCommandExecuted(command);               break;
            }
        }

        private async Task HelpCommandExecuted(SocketSlashCommand command)
        {
            await _genericCommandsService.HelpCommandExecuted(command);
        }

        private async Task StartRaidCommandExecuted(SocketSlashCommand command)
        {
            await _raidService.StartRaid(command, _client);
        }

        private async Task CloseRaidCommandExecuted(SocketSlashCommand command)
        {
            await _raidService.CloseRaid(command, _client);
        }

        private async Task ReopenRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.ReopenRaffleCommandExecuted(command, _client);
        }

        private async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.ReopenEventRaffleCommandExecuted(command, _client);
        }

        private async Task CompleteRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.CompleteRaffleCommandExecuted(command, _client);
        }

        private async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.CompleteEventRaffleCommandExecuted(command, _client);
        }

        private async Task RaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.RaffleCommandExecuted(command, _client);
        }

        private async Task EventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.EventRaffleCommandExecuted(command, _client);
        }

        private async Task CreateRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.CreateRaffleCommandExecuted(command, _client);
        }

        private async Task CreateEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommandsService.CreateEventRaffleCommandExecuted(command, _client);
        }

        private async Task VerifyCommandExecuted(SocketSlashCommand command)
        {
            await _verifyCommandsService.VerifyCommandExecuted(command, _client);
        }

        private async Task DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await _verifyCommandsService.DeverifyCommandExecuted(command, _client);
        }

        private async Task PointsCommandExecuted(SocketSlashCommand command)
        {
            await _pointsCommandsService.PointsCommandExecuted(command);
        }

        private async Task SetLogChannel(SocketSlashCommand command)
        {
            await _discordCommandService.SetLogChannel(command, _client);
        }

        private async Task DefaultCommandExecuted(SocketSlashCommand command)
        {
            await command.RespondAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
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
