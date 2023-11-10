using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using GW2DonBot.Models;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Services.CacheServices;
using Services.DiscordRequestServices;
using Services.LogGenerationServices;
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
        private readonly IGenericCommands _genericCommands;
        private readonly IVerifyCommands _verifyCommands;
        private readonly IPointsCommands _pointsCommands;
        private readonly IRaffleCommands _raffleCommands;
        private readonly IPollingTasks _pollingTasks;
        private readonly IDataModelGenerationService _dataModelGenerator;

        private readonly DatabaseContext _databaseContext;
        private readonly DiscordSocketClient _client;

        public DiscordCore(
            ISecretService secretService,
            ILoggingService loggingService,
            ICacheService cacheService,
            IMessageGenerationService messageGenerationService,
            IGenericCommands genericCommands,
            IVerifyCommands verifyCommands,
            IPointsCommands pointsCommands,
            IRaffleCommands raffleCommands,
            IPollingTasks pollingTasks,
            IDatabaseContext databaseContext,
            IDataModelGenerationService dataModelGenerator)
        {
            _secretService = secretService;
            _loggingService = loggingService;
            _cacheService = cacheService;
            _messageGenerationService = messageGenerationService;
            _genericCommands = genericCommands;
            _verifyCommands = verifyCommands;
            _pointsCommands = pointsCommands;
            _raffleCommands = raffleCommands;
            _pollingTasks = pollingTasks;
            _dataModelGenerator = dataModelGenerator;
            _databaseContext = databaseContext.GetDatabaseContext();

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
            await _genericCommands.HelpCommandExecuted(command);
        }

        private async Task ReopenRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.ReopenRaffleCommandExecuted(command, _client);
        }

        private async Task ReopenEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.ReopenEventRaffleCommandExecuted(command, _client);
        }

        private async Task CompleteRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.CompleteRaffleCommandExecuted(command, _client);
        }

        private async Task CompleteEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.CompleteEventRaffleCommandExecuted(command, _client);
        }

        private async Task RaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.RaffleCommandExecuted(command, _client);
        }

        private async Task EventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.EventRaffleCommandExecuted(command, _client);
        }

        private async Task CreateRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.CreateRaffleCommandExecuted(command, _client);
        }

        private async Task CreateEventRaffleCommandExecuted(SocketSlashCommand command)
        {
            await _raffleCommands.CreateEventRaffleCommandExecuted(command, _client);
        }

        private async Task VerifyCommandExecuted(SocketSlashCommand command)
        {
            await _verifyCommands.VerifyCommandExecuted(command, _client);
        }

        private async Task DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await _verifyCommands.DeverifyCommandExecuted(command, _client);
        }

        private async Task PointsCommandExecuted(SocketSlashCommand command)
        {
            await _pointsCommands.PointsCommandExecuted(command);
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
            _pollingTasks.PollingRoles(_client);
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            try
            {
                if (!(seenMessage.Channel is SocketGuildChannel channel))
                {
                    Console.WriteLine($"Did not find user {seenMessage.Author.Username} in guild");
                    return;
                }

                var guild = await _databaseContext.Guild.FirstOrDefaultAsync(g => g.GuildId == (long)channel.Guild.Id);
                if (guild == null || !guild.DebugWebhookChannelId.HasValue || !guild.WebhookChannelId.HasValue)
                {
                    Console.WriteLine($"Unable to find guild {channel.Guild.Id} or empty value for guild.DebugWebhookChannelId '{guild?.DebugWebhookChannelId}' or guild.WebhookChannelId '{guild?.WebhookChannelId}'");
                    return;
                }

                // Ignore messages outside webhook, in upload channel, or from Don
                if (seenMessage.Source != MessageSource.Webhook || 
                    (seenMessage.Channel.Id != (ulong)guild.DebugWebhookChannelId && seenMessage.Channel.Id != (ulong)guild.WebhookChannelId) || 
                    seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
                {
                    return;
                }

                var webhookUrl = seenMessage.Channel.Id == (ulong)guild.DebugWebhookChannelId ? guild.DebugWebhook : guild.Webhook;

                var urls = seenMessage.Embeds.SelectMany(x => x.Fields.SelectMany(y => y.Value.Split('('))).Where(x => x.Contains(")")).ToList();
                urls.AddRange(seenMessage.Embeds.Select(x => x.Url).Where(x => !string.IsNullOrEmpty(x)));

                var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

                foreach (var url in trimmedUrls)
                {
                    Console.WriteLine($"[DON] Assessing: {url}");
                    AnalyseAndReportOnUrl(url, channel.Guild.Id, channel.Guild, webhookUrl);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to parse user as socket guild user. Did not find user {seenMessage.Author.Username} in guild. Error: {e.Message}");
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
            var data = await _dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url);

            if (data.Success == false)
            {
                return;
            }

            var guilds = await _databaseContext.Guild.ToListAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)guildId);

            if (guild == null)
            {
                return;
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
