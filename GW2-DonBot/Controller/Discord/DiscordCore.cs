﻿using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Models.Statics;
using Services.CacheServices;
using Services.DiscordRequestServices;
using Services.LogGenerationServices;
using Services.Logging;
using Services.PlayerServices;
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
        private readonly IGenericCommandsService _genericCommandsService;
        private readonly IVerifyCommandsService _verifyCommandsService;
        private readonly IPointsCommandsService _pointsCommandsService;
        private readonly IRaffleCommandsService _raffleCommandsService;
        private readonly IPollingTasksService _pollingTasksService;
        private readonly IPlayerService _playerService;
        private readonly IDataModelGenerationService _dataModelGenerator;
        private readonly IRaidService _raidService;

        private readonly DatabaseContext _databaseContext;
        private readonly DiscordSocketClient _client;

        public DiscordCore(
            ISecretService secretService,
            ILoggingService loggingService,
            ICacheService cacheService,
            IMessageGenerationService messageGenerationService,
            IGenericCommandsService genericCommandsService,
            IVerifyCommandsService verifyCommandsService,
            IPointsCommandsService pointsCommandsService,
            IRaffleCommandsService raffleCommandsService,
            IPollingTasksService pollingTasksService,
            IPlayerService playerService,
            IRaidService raidService,
            IDataModelGenerationService dataModelGenerator,
            DatabaseContext databaseContext)
        {
            _secretService = secretService;
            _loggingService = loggingService;
            _cacheService = cacheService;
            _messageGenerationService = messageGenerationService;
            _genericCommandsService = genericCommandsService;
            _verifyCommandsService = verifyCommandsService;
            _pointsCommandsService = pointsCommandsService;
            _raffleCommandsService = raffleCommandsService;
            _pollingTasksService = pollingTasksService;
            _playerService = playerService;
            _raidService = raidService;
            _dataModelGenerator = dataModelGenerator;
            _databaseContext = databaseContext;

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

            //await RegisterCommands(_client);
            _client.MessageReceived += MessageReceivedAsync;
            _client.Log += _loggingService.Log;
            _client.SlashCommandExecuted += SlashCommandExecutedAsync;
            _client.ButtonExecuted += async (buttonComponent) =>
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
                    default:
                        break;
                }
            };

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

                var startRaid = new SlashCommandBuilder()
                    .WithName("start_raid")
                    .WithDescription("Starts raid.");

                await guild.CreateApplicationCommandAsync(startRaid.Build());

                var closeRaid = new SlashCommandBuilder()
                    .WithName("close_raid")
                    .WithDescription("Closes raid.");

                await guild.CreateApplicationCommandAsync(closeRaid.Build());
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
                default:                                await DefaultCommandExecuted(command);               break;
            }
        }

        private async Task HelpCommandExecuted(SocketSlashCommand command)
        {
            await _genericCommandsService.HelpCommandExecuted(command);
        }

        private async Task StartRaidCommandExecuted(SocketSlashCommand command)
        {
            await _raidService.StartRaid(command);
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

        private async Task DefaultCommandExecuted(SocketSlashCommand command)
        {
            await command.RespondAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
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
            await _pollingTasksService.PollingRoles(_client);
        }

        private Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            try
            {
                if (seenMessage.Channel is not SocketGuildChannel channel)
                {
                    Console.WriteLine($"Did not find user {seenMessage.Author.Username} in guild");
                    return Task.CompletedTask;
                }

                var guild = _databaseContext.Guild.FirstOrDefaultAsync(g => g.GuildId == (long)channel.Guild.Id).Result;
                if (guild == null || !guild.LogDropOffChannelId.HasValue)
                {
                    Console.WriteLine($"Unable to find guild {channel.Guild.Id}");
                    return Task.CompletedTask;
                }

                // Ignore messages outside webhook, in upload channel, or from Don
                if (seenMessage.Source != MessageSource.Webhook || 
                    (seenMessage.Channel.Id != (ulong)guild.LogDropOffChannelId) || 
                    seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
                {
                    return Task.CompletedTask;
                }

                var urls = seenMessage.Embeds.SelectMany(x => x.Fields.SelectMany(y => y.Value.Split('('))).Where(x => x.Contains(")")).ToList();
                urls.AddRange(seenMessage.Embeds.Select(x => x.Url).Where(x => !string.IsNullOrEmpty(x)));

                var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

                foreach (var url in trimmedUrls)
                {
                    Console.WriteLine($"[DON] Assessing: {url}");
                    AnalyseAndReportOnUrl(url, channel.Guild.Id).Wait();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to parse user as socket guild user. Did not find user {seenMessage.Author.Username} in guild. Error: {e.Message}");
            }

            return Task.CompletedTask;
        }

        private async Task AnalyseAndReportOnUrl(string url, ulong guildId)
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
            var data = await _dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url);

            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildId);

            if (guild == null)
            {
                return;
            }

            Console.WriteLine($"[DON] Generating fight summary: {url}");

            if (guild.LogReportChannelId == null)
            {
                Console.WriteLine($"[DON] no log report channel id for guild id `{guild.GuildId}`");
                return;
            }

            if (_client.GetChannel((ulong)guild.LogReportChannelId) is not ITextChannel logReportChannel)
            {
                Console.WriteLine($"[DON] Failed to find the target channel {guild.LogReportChannelId}");
                return;
            }

            Embed message;
            if (data.Wvw)
            {
                if (guild.AdvanceLogReportChannelId != null)
                {
                    if (_client.GetChannel((ulong)guild.AdvanceLogReportChannelId) is not ITextChannel advanceLogReportChannel)
                    {
                        Console.WriteLine($"[DON] Failed to find the target channel {guild.AdvanceLogReportChannelId}");
                        return;
                    }

                    var advancedMessage = _messageGenerationService.GenerateWvWFightSummary(data, true, guild, _client);
                    await advanceLogReportChannel.SendMessageAsync(text: "", embeds: new[] { advancedMessage });
                }

                message = _messageGenerationService.GenerateWvWFightSummary(data, false, guild, _client);
                await _playerService.SetPlayerPoints(data);

                if (guild.PlayerReportChannelId != null && _client.GetChannel((ulong)guild.PlayerReportChannelId) is SocketTextChannel playerChannel)
                {
                    var messages = await playerChannel.GetMessagesAsync(10).FlattenAsync();
                    await playerChannel.DeleteMessagesAsync(messages);

                    var activePlayerMessage = await _messageGenerationService.GenerateWvWActivePlayerSummary(guild, url);
                    await playerChannel.SendMessageAsync(text: "", embeds: new[] { activePlayerMessage });

                    var playerMessage = await _messageGenerationService.GenerateWvWPlayerSummary(guild);
                    await playerChannel.SendMessageAsync(text: "", embeds: new[] { playerMessage });
                }
            }
            else
            {
                message = _messageGenerationService.GeneratePvEFightSummary(data, (long)guildId);
            }

            await logReportChannel.SendMessageAsync(text: "", embeds: new[] { message });
            Console.WriteLine($"[DON] Completed and posted report on: {url}");
        }
    }
}
