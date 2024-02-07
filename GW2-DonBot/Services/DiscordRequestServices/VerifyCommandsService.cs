using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Models.GW2Api;
using Newtonsoft.Json;

namespace Services.DiscordRequestServices
{
    public class VerifyCommandsService : IVerifyCommandsService
    {
        private readonly DatabaseContext _databaseContext;

        public VerifyCommandsService(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public async Task VerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the response to avoid timeouts
            await command.DeferAsync(ephemeral: true);

            // Check if the command was executed in a guild
            if (command.GuildId == null)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to verify, please try again.");
                return;
            }

            // Get the guild user
            var guildUser = discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id);

            // Get the API key from the command options
            var apiKey = command.Data.Options?.FirstOrDefault()?.Value?.ToString();

            // Check if the API key is null or empty
            if (string.IsNullOrEmpty(apiKey))
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to verify, please try again.");
                return;
            }

            // Call the GW2 API to get the account data
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");

            // Check if the API call was successful
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[DON] API call success");

                // Deserialize the account data
                var stringData = await response.Content.ReadAsStringAsync();
                var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                // Check if the account already exists in the database
                var isNewAccount = false;
                var account = await _databaseContext.Account.FirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);

                if (account != null)
                {
                    // Update the existing account
                    account.Gw2AccountId = accountData.Id;
                    account.Gw2AccountName = accountData.Name;
                    account.Gw2ApiKey = apiKey;

                    _databaseContext.Update(account);
                }
                else
                {
                    // Create a new account
                    isNewAccount = true;
                    account = new Account()
                    {
                        DiscordId = (long)command.User.Id,
                        Gw2AccountId = accountData.Id,
                        Gw2AccountName = accountData.Name,
                        Gw2ApiKey = apiKey
                    };
                    _databaseContext.Add(account);
                }

                await _databaseContext.SaveChangesAsync();

                // Get the guild from the database
                var guild = await _databaseContext.Guild.FirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);

                if (guild == null)
                {
                    return;
                }

                // Build the output message
                var output = "";
                output += isNewAccount ?
                          $"Verify succeeded! New GW2 account registered: `{accountData.Name}`\n" :
                          $"Verify succeeded! GW2 account updated: `{accountData.Name}`\n";

                output += "Verify role has been assigned!\n";

                // Check if the user is in the primary or secondary guild
                var primaryGuildId = guild.Gw2GuildMemberRoleId;
                var secondaryGuildIds = guild.Gw2SecondaryMemberRoleIds?.Split(',');

                var inPrimaryGuild = accountData.Guilds.Contains(primaryGuildId);
                var inSecondaryGuild = secondaryGuildIds?.Any(guildId => accountData.Guilds.Contains(guildId)) ?? false;

                output += inPrimaryGuild ?
                          "User is in `Standard of Heroes` - SoX roles have been assigned! :heart:" :
                          inSecondaryGuild ?
                          "User is in an Alliance guild - Alliance roles have been assigned! :heart:" :
                          "User is not in `Standard of Heroes` or a valid Alliance guild - special roles denied! :broken_heart:\nPlease contact Squirrel or an officer if this is incorrect!";

                // Assign the roles to the user
                AssignRoles(guildUser, guild, inPrimaryGuild, inSecondaryGuild);

                // Edit the response message with the output
                await command.ModifyOriginalResponseAsync(message => message.Content = output);
            }
            else
            {
                Console.WriteLine("[DON] API call failed");

                await command.ModifyOriginalResponseAsync(message => message.Content = $"Looks like you screwed up a couple of letters in the api key, try again mate, failed to process with API key: `{apiKey}`");
            }
        }

        public async Task DeverifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Defer the response to avoid timeouts
            await command.DeferAsync(ephemeral: true);

            // Check if the account exists in the database
            var accountFound = false;
            var output = "";
            var model = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            var account = model.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

            if (account != null)
            {
                accountFound = true;
                account.Gw2ApiKey = null;
                _databaseContext.Update(account);
            }

            await _databaseContext.SaveChangesAsync();

            // Generate output message
            output += accountFound ?
                $"Deverify succeeded! Account data cleared for: `{command.User}`" :
                $"Deverify unnecessary! No account data found for: `{command.User}`";

            // Check if the guild exists in the database
            var guilds = await _databaseContext.Guild.ToListAsync();
            if (command.GuildId == null) {
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to deverify, make sure you asking withing a discord server.");
                return;
            }

            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)command.GuildId);

            if (guild == null)
            {
                await command.ModifyOriginalResponseAsync(message => message.Content = output);
                return;
            }

            // Get the guild user
            SocketGuildUser guildUser;
            try
            {
                guildUser = discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failing deverify nicely: `{ex.Message}`");
                await command.ModifyOriginalResponseAsync(message => message.Content = "Failed to deverify, please try again.");
                return;
            }

            // Remove roles from the user
            RemoveRoles(guildUser, guild);

            // Modify the response with the output message
            await command.ModifyOriginalResponseAsync(message => message.Content = output);
        }

        private async void AssignRoles(SocketGuildUser guildUser, Guild guild, bool inPrimaryGuild, bool inSecondaryGuild)
        {
            // Get the roles from the guild
            var primaryRoleId = guild.DiscordGuildMemberRoleId;
            var secondaryRoleId = guild.DiscordSecondaryMemberRoleId;
            var verifiedRoleId = guild.DiscordVerifiedRoleId;

            // Check if the roles are null
            if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
            {
                await guildUser.SendMessageAsync("Roles are not set up on this server, cannot proceed.");
                return;
            }

            var primaryRole = guildUser.Guild.GetRole((ulong)primaryRoleId);
            var secondaryRole = guildUser.Guild.GetRole((ulong)secondaryRoleId);
            var verifiedRole = guildUser.Guild.GetRole((ulong)verifiedRoleId);

            // Assign the roles to the user
            if (inPrimaryGuild)
            {
                await guildUser.AddRoleAsync(primaryRole);
            }

            if (inSecondaryGuild)
            {
                await guildUser.AddRoleAsync(secondaryRole);
            }

            await guildUser.AddRoleAsync(verifiedRole);
        }

        private async void RemoveRoles(SocketGuildUser guildUser, Guild guild)
        {
            // Get the roles from the guild
            var primaryRoleId = guild.DiscordGuildMemberRoleId;
            var secondaryRoleId = guild.DiscordSecondaryMemberRoleId;
            var verifiedRoleId = guild.DiscordVerifiedRoleId;

            // Check if the roles are null
            if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
            {
                await guildUser.SendMessageAsync("Roles are not set up on this server, cannot proceed.");
                return;
            }

            var user = (IGuildUser)guildUser;
            var primaryRole = ((IGuildChannel)guildUser.VoiceChannel).Guild.GetRole((ulong)primaryRoleId);
            var secondaryRole = ((IGuildChannel)guildUser.VoiceChannel).Guild.GetRole((ulong)secondaryRoleId);
            var verifiedRole = ((IGuildChannel)guildUser.VoiceChannel).Guild.GetRole((ulong)verifiedRoleId);

            // Remove the roles from the user
            if (user.RoleIds.ToList().Contains((ulong)primaryRoleId))
            {
                await user.RemoveRoleAsync(primaryRole);
            }

            if (user.RoleIds.ToList().Contains((ulong)secondaryRoleId))
            {
                await user.RemoveRoleAsync(secondaryRole);
            }

            if (user.RoleIds.ToList().Contains((ulong)verifiedRoleId))
            {
                await user.RemoveRoleAsync(verifiedRole);
            }
        }
    }
}