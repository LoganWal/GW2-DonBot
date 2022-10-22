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
using System.Data;
using ConnectionState = Discord.ConnectionState;

namespace Controller.Discord
{
    public class DiscordCore: IDiscordCore
    {
        private readonly ISecretService _secretService;
        private readonly ILoggingService _loggingService;
        private readonly ICacheService _cacheService;
        private readonly IMessageGenerationService _messageGenerationService;

        private Dictionary<string, string> _settings;

        public DiscordCore(ISecretService secretService, ILoggingService loggingService, ICacheService cacheService, IMessageGenerationService messageGenerationService)
        {
            _secretService = secretService;
            _loggingService = loggingService;
            _cacheService = cacheService;
            _messageGenerationService = messageGenerationService;

            _settings = _secretService.FetchAll();
        }

        public async Task MainAsync()
        {
            // Loading secrets

            // Initialization
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            var client = new DiscordSocketClient(config);

            // Logging in...
            await client.LoginAsync(TokenType.Bot, _settings[nameof(BotSecretsDataModel.BotToken)]);
            await client.StartAsync();

            Console.WriteLine($"[DON] GW2-DonBot attempting to connect...");
            while (client.ConnectionState != ConnectionState.Connected)
            {
                await Task.Delay(100);
            }
            Console.WriteLine($"[DON] GW2-DonBot connected in");

            await RegisterCommands(client);

            client.MessageReceived += MessageReceivedAsync;
            client.Log += _loggingService.Log;
            client.SlashCommandExecuted += SlashCommandExecutedAsync;

#if DEBUG
            await AnalyseDebugUrl();
#endif

            Console.WriteLine($"[DON] GW2-DonBot setup - ready to cause chaos");

            // Block this task until the program is closed.
            await Task.Delay(-1);

            // Safely close...
            client.Log -= _loggingService.Log;
            client.MessageReceived -= MessageReceivedAsync;
            client.SlashCommandExecuted -= SlashCommandExecutedAsync;
        }

        private async Task RegisterCommands(DiscordSocketClient client)
        { 
            var guild = client.GetGuild(ulong.Parse(_settings[nameof(BotSecretsDataModel.WvWGuildId)])); 

            // Guild commands
            var helpGuildCommand = new SlashCommandBuilder()
            .WithName("help")
            .WithDescription("List out DonBot's commands and how to use them.");

            await guild.CreateApplicationCommandAsync(helpGuildCommand.Build());

            var roleFixGuildCommand = new SlashCommandBuilder()
            .WithName("role-fix")
            .WithDescription("Admin command: Fixes WvW and Alliance member roles based on verify.")
            .AddOption("password", ApplicationCommandOptionType.String, "Required password", isRequired: true);

            await guild.CreateApplicationCommandAsync(roleFixGuildCommand.Build());

            var deverifyGuildCommand = new SlashCommandBuilder()
            .WithName("deverify")
            .WithDescription("Remove any /verify information stored for your Discord account.");

            await guild.CreateApplicationCommandAsync(deverifyGuildCommand.Build());

            // Global commands
            var verifyCommand = new SlashCommandBuilder()
            .WithName("verify")
            .WithDescription("Verify your GW2 API key so that your GW2 Account and Discord are linked.")
            .AddOption("api-key", ApplicationCommandOptionType.String, "The API key you wish to link", isRequired: true);

            await client.CreateGlobalApplicationCommandAsync(verifyCommand.Build());
        }

        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "help":            await HelpCommandExecuted(command); break;
                
                case "verify":          await VerifyCommandExecuted(command); break;
                case "deverify":        await DeverifyCommandExecuted(command); break;
                case "role-fix":        await OneOffRoleFixCommandExecuted(command); break;

                default:                await DefaultCommandExecuted(command); break;
            }
        }

        private async Task HelpCommandExecuted(SocketSlashCommand command)
        {
            var message = "";
            message += $"**/help**";
            message += $"\n*The output of this command will only be visible to you.*";
            message += $"\nThis is where you are now! Use this to get help on how some commands work.";

            message += $"\n\n**/verify**";
            message += $"\n*The output of this command will only be visible to you.*";
            message += $"\nThis command can be used to link your GW2 and Discord accounts via a GW2 API key! ";
            message += $"This is required to have access to some roles, and will give you access to future features once they're developed! ";
            message += $"Once verified, you won't need to use this command again unless you wish to update your details.";
            message += $"\n`[api-key]` This is your GW2 API key, make sure it has guild and account permissions!";

            message += $"\n\n**/deverify**";
            message += $"\n*The output of this command will only be visible to you.*";
            message += $"\nThis command can be used to remove any currently stored data associated with your Discord account. ";
            message += $"The data stored via the /verify command can be wiped through this. Note you will have to re-verify to access certain roles and features! ";
            message += $"This will only remove the information associated with the Discord account used to trigger the command.";

            await command.RespondAsync(message, ephemeral: true);
        }

        private async Task VerifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            var apiKey = command.Data.Options.First().Value.ToString();

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DON] API call success");

                var stringData = await response.Content.ReadAsStringAsync();
                var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                bool isNewAccount = false;

                using (var context = new DatabaseContext().SetSecretService(_secretService))
                {
                    var model = await context.Account.ToListAsync();
                    var account = model.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);
                    
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
                }

                var output = "";
                output += isNewAccount ?
                          $"Verify succeeded! New GW2 account registered: `{accountData.Name}`\n" :
                          $"Verify succeeded! GW2 account updated: `{accountData.Name}`\n";

                output += "Verify role has been assigned!\n";

                var primaryGuildId = _settings[nameof(BotSecretsDataModel.WvWPrimaryGuildId)];
                var secondaryGuildIds = _settings[nameof(BotSecretsDataModel.WvWSecondaryGuildIds)].Split(',');
                
                bool inPrimaryGuild = accountData.Guilds.Contains(primaryGuildId);
                bool inSecondaryGuild = false;
                foreach (var guildId in secondaryGuildIds)
                {
                    if (accountData.Guilds.Contains(guildId))
                    {
                        inSecondaryGuild = true;
                        break;
                    }
                }
                
                output += inPrimaryGuild ?
                          "User is in `Standard of Heroes` - SoX roles have been assigned! :heart:" :
                          inSecondaryGuild ?
                          "User is in an Alliance guild - Alliance roles have been assigned! :heart:" :
                          "User is not in `Standard of Heroes` or a valid Alliance guild - special roles denied! :broken_heart:\nPlease contact Squirrel or an officer if this is incorrect!";
                
                // Adds verified role
                var primaryRoleId = _settings[nameof(BotSecretsDataModel.WvWMemberRoleId)];
                var secondaryRoleId = _settings[nameof(BotSecretsDataModel.WvWAllianceMemberRoleId)];
                var user = (IGuildUser)command.User;
                var primaryRole = ((IGuildChannel)command.Channel).Guild.GetRole(ulong.Parse(primaryRoleId));
                var secondaryRole = ((IGuildChannel)command.Channel).Guild.GetRole(ulong.Parse(secondaryRoleId));

                if (inPrimaryGuild)
                {
                    await user.AddRoleAsync(primaryRole);
                }

                if (inSecondaryGuild)
                {
                    await user.AddRoleAsync(secondaryRole);
                }

                // Edit message to send through the actual filled out message
                await command.ModifyOriginalResponseAsync(message => message.Content = output);
            }
            else
            {
                Console.WriteLine($"[DON] API call failed");

                await command.ModifyOriginalResponseAsync(message => message.Content = $"Verify failed to process with API key: `{apiKey}`");
            }
        }

        private async Task DeverifyCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            bool accountFound = false;

            // TODO: hahaha
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
            }

            var output = "";
            output += accountFound ?
                      $"Deverify succeeded! Account data cleared for: `{command.User}`" :
                      $"Deverify unnecessary! No account data found for: `{command.User}`";

            // Removes roles
            var primaryRoleId = _settings[nameof(BotSecretsDataModel.WvWMemberRoleId)];
            var secondaryRoleId = _settings[nameof(BotSecretsDataModel.WvWAllianceMemberRoleId)];
            var user = (IGuildUser)command.User;
            var primaryRole = ((IGuildChannel)command.Channel).Guild.GetRole(ulong.Parse(primaryRoleId));
            var secondaryRole = ((IGuildChannel)command.Channel).Guild.GetRole(ulong.Parse(secondaryRoleId));

            if (user.RoleIds.ToList().Contains(ulong.Parse(primaryRoleId)))
            {
                await user.RemoveRoleAsync(primaryRole);
                output += $"\nRemoved `{primaryRole.Name}` role.";
            }

            if (user.RoleIds.ToList().Contains(ulong.Parse(secondaryRoleId)))
            {
                await user.RemoveRoleAsync(secondaryRole);
                output += $"\nRemoved `{primaryRole.Name}` role.";
            }

            await command.ModifyOriginalResponseAsync(message => message.Content = output);
        }

        private async Task OneOffRoleFixCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            var password = command.Data.Options.First().Value;
            if (string.Compare((string)password, _settings[nameof(BotSecretsDataModel.WvWCommandPassword)], StringComparison.OrdinalIgnoreCase) != 0)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = $"Incorrect password. This attempt has been logged.");
                return;
            }

            var primaryRoleId = _settings[nameof(BotSecretsDataModel.WvWMemberRoleId)];
            var secondaryRoleId = _settings[nameof(BotSecretsDataModel.WvWAllianceMemberRoleId)];
            var primaryRole = ((IGuildChannel)command.Channel).Guild.GetRole(ulong.Parse(primaryRoleId));
            var secondaryRole = ((IGuildChannel)command.Channel).Guild.GetRole(ulong.Parse(secondaryRoleId));

            var guild = ((IGuildChannel)command.Channel).Guild;
            var guildUsers = await guild.GetUsersAsync();

            var output = "";
            var primaryUserCountRemoved = 0;
            var secondaryUserCountRemoved = 0;
            var primaryUserCountAdded = 0;
            var secondaryUserCountAdded = 0;
            
            // Role removal (strips roles from everybody in the guild)
            foreach (var user in guildUsers)
            {
                if (user.RoleIds.ToList().Contains(ulong.Parse(primaryRoleId)))
                {
                    await user.RemoveRoleAsync(primaryRole);
                    primaryUserCountRemoved++;
                }

                if (user.RoleIds.ToList().Contains(ulong.Parse(secondaryRoleId)))
                {
                    await user.RemoveRoleAsync(secondaryRole);
                    secondaryUserCountRemoved++;
                }
            }

            output += $"Removed `{primaryRole.Name}` role from `{primaryUserCountRemoved}` players.\n";
            output += $"Removed `{secondaryRole.Name}` role from `{secondaryUserCountRemoved}` players.\n";

            // Role add based on db (adds roles to everyone who is verified)
            using (var context = new DatabaseContext().SetSecretService(_secretService))
            {
                var accounts = await context.Account.ToListAsync();

                foreach (var account in accounts)
                {
                    var apiKey = account.Gw2ApiKey;

                    var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[DON] API call success");

                        var stringData = await response.Content.ReadAsStringAsync();
                        var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                        var primaryGuildId = _settings[nameof(BotSecretsDataModel.WvWPrimaryGuildId)];
                        var secondaryGuildIds = _settings[nameof(BotSecretsDataModel.WvWSecondaryGuildIds)].Split(',');

                        bool inPrimaryGuild = accountData.Guilds.Contains(primaryGuildId);
                        bool inSecondaryGuild = false;

                        foreach (var guildId in secondaryGuildIds)
                        {
                            if (accountData.Guilds.Contains(guildId))
                            {
                                inSecondaryGuild = true;
                                break;
                            }
                        }

                        var user = guildUsers.FirstOrDefault(s => s.Id == (ulong)account.DiscordId);
                        if (user == null)
                        {
                            continue;
                        }

                        if (inPrimaryGuild)
                        {
                            await user.AddRoleAsync(primaryRole);
                            primaryUserCountAdded++;
                        }

                        if (inSecondaryGuild)
                        {
                            await user.AddRoleAsync(secondaryRole);
                            secondaryUserCountAdded++;
                        }
                    }
                }
            }

            output += $"Added `{primaryRole.Name}` role to `{primaryUserCountAdded}` players.\n";
            output += $"Added `{secondaryRole.Name}` role to `{secondaryUserCountAdded}` players.";

            await command.ModifyOriginalResponseAsync(message => message.Content = output);
        }

        private async Task DefaultCommandExecuted(SocketSlashCommand command)
        {
            await command.RespondAsync($"The command `{command.Data.Name}` is not implemented.", ephemeral: true);
        }

        private async Task AnalyseDebugUrl()
        {
            var debugUrl = "";
            if (debugUrl == "")
            {
                return;
            }
            var webhook = new DiscordWebhookClient(_settings[nameof(BotSecretsDataModel.WvWDebugWebhookUrl)]); // TODO: change webhook url based on context!
            await AnalyseAndReportOnUrl(webhook, debugUrl);
        }

        private async Task MessageReceivedAsync(SocketMessage seenMessage)
        {
            // Ignore outside webhook + in upload channel + from Don
            if (seenMessage.Source != MessageSource.Webhook || 
                (seenMessage.Channel.Id != ulong.Parse(_settings[nameof(BotSecretsDataModel.WvWDebugUploadChannelId)]) &&
                seenMessage.Channel.Id != ulong.Parse(_settings[nameof(BotSecretsDataModel.WvWUploadChannelId)]) && 
                seenMessage.Channel.Id != ulong.Parse(_settings[nameof(BotSecretsDataModel.PvEDebugUploadChannelId)]) && 
                seenMessage.Channel.Id != ulong.Parse(_settings[nameof(BotSecretsDataModel.PvEUploadChannelId)])) || 
                seenMessage.Author.Username.Contains("GW2-DonBot", StringComparison.OrdinalIgnoreCase)) 
            {
                return;
            }
           
            var webhook = new DiscordWebhookClient(_settings[nameof(BotSecretsDataModel.WvWWebhookUrl)]); // TODO: break this apart so it can handle multiple webhooks

            var urls = seenMessage.Embeds.SelectMany((x => x.Fields.SelectMany(y => y.Value.Split('(')))).Where(x => x.Contains(")")).ToList();

            var trimmedUrls = urls.Select(url => url.Contains(')') ? url[..url.IndexOf(')')] : url).ToList();

            foreach (var url in trimmedUrls)
            {
                Console.WriteLine($"[DON] Assessing: {url}");
                await AnalyseAndReportOnUrl(webhook, url);
            }
        }

        private async Task AnalyseAndReportOnUrl(DiscordWebhookClient webhook, string url)
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
            var data = dataModelGenerator.GenerateEliteInsightDataModelFromUrl(url);

            var message = _messageGenerationService.GenerateWvWFightSummary(data);

            await webhook.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { message });
            Console.WriteLine($"[DON] Completed and posted report on: {url}");
        }
    }
}
