using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using GW2DonBot.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.GW2Api;
using Models.Entities;
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

        private readonly Dictionary<string, string> _settings;

        private readonly DiscordSocketClient _client;

        public DiscordCore(ISecretService secretService, ILoggingService loggingService, ICacheService cacheService, IMessageGenerationService messageGenerationService)
        {
            _secretService = secretService;
            _loggingService = loggingService;
            _cacheService = cacheService;
            _messageGenerationService = messageGenerationService;

            _settings = _secretService.FetchAll();

            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            _client = new DiscordSocketClient(config);
        }

        public async Task MainAsync()
        {
            // Logging in...
            await _client.LoginAsync(TokenType.Bot, _settings[nameof(BotSecretsDataModel.BotToken)]);
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
            var guilds = client.Guilds;
            foreach (var guild in guilds)
            {
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
                    .AddOption("api-key", ApplicationCommandOptionType.String, "The API key you wish to link", isRequired: true);

                await guild.CreateApplicationCommandAsync(verifyCommand.Build());
            }
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "help":            await HelpCommandExecuted(command); break;
                
                case "verify":          await VerifyCommandExecuted(command); break;
                case "deverify":        await DeverifyCommandExecuted(command); break;

                default:                await DefaultCommandExecuted(command); break;
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
                Console.WriteLine($"[DON] API call success");

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
                Console.WriteLine($"[DON] API call failed");

                await command.ModifyOriginalResponseAsync(message => message.Content = $"Looks like you screwed up a couple of letters in the api key, try again mate, failed to process with API key: `{apiKey}`");
            }
        }

        private async Task DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            var accountFound = false;

            Guild? guild;
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var model = await context.Account.ToListAsync();
                var account = model.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

                if (account != null)
                {
                    accountFound = true;
                    context.Remove(account);
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
            output += accountFound ?
                      $"Deverify succeeded! Account data cleared for: `{command.User}`" :
                      $"Deverify unnecessary! No account data found for: `{command.User}`";

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
                var accounts = await context.Account.ToListAsync();
                var guilds = await context.Guild.ToListAsync();

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
                    foreach (var user in guildUsers)
                    {
                        var account = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
                        if (account == null)
                        {
                            continue;
                        }

                        var apiKey = account.Gw2ApiKey;

                        var httpClient = new HttpClient();
                        var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"=== Handling {account.Gw2AccountName.Trim()} : {user.DisplayName.Trim()} ===");

                            var stringData = await response.Content.ReadAsStringAsync();
                            var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                            var primaryGuildId = guildConfiguration.Gw2GuildMemberRoleId;
                            var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds.Split(',');

                            var inPrimaryGuild = accountData.Guilds.Contains(primaryGuildId);
                            var inSecondaryGuild = secondaryGuildIds.Any(guildId => accountData.Guilds.Contains(guildId));

                            var roles = user.Roles.Select(s => s.Id).ToList();
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
            }
        }

        private async Task DefaultCommandExecuted(SocketSlashCommand command)
        {
            await command.RespondAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            var user = seenMessage.Author;
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
                var webhook = new DiscordWebhookClient(webhookUrl);

                var urls = seenMessage.Embeds.SelectMany((x => x.Fields.SelectMany(y => y.Value.Split('(')))).Where(x => x.Contains(")")).ToList();
                urls.AddRange(seenMessage.Embeds.Select(x => x.Url).Where(x => !string.IsNullOrEmpty(x)));

                var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

                foreach (var url in trimmedUrls)
                {
                    Console.WriteLine($"[DON] Assessing: {url}");
                    AnalyseAndReportOnUrl(webhook, url, guildUser.Id);
                }
            }
        }

        private async Task AnalyseAndReportOnUrl(DiscordWebhookClient webhook, string url, ulong guildId)
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

            Console.WriteLine($"[DON] Generating fight summary: {url}");
            var message = _messageGenerationService.GenerateFightSummary(data, guildId);

            await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            Console.WriteLine($"[DON] Completed and posted report on: {url}");
        }
    }
}
