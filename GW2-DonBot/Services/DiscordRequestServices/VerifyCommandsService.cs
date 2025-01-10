using Discord;
using Discord.WebSocket;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.DiscordRequestServices
{
    public class VerifyCommandsService(
        DatabaseContext databaseContext,
        ILogger<VerifyCommandsService> logger,
        IHttpClientFactory httpClientFactory)
        : IVerifyCommandsService
    {
        public async Task VerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Check if the command was executed in a guild
            if (command.GuildId == null)
            {
                await command.FollowupAsync("Failed to verify, please try again.", ephemeral: true);
                return;
            }

            // Get the guild user
            var guildUser = discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id);

            // Get the API key from the command options
            var apiKey = command.Data.Options?.FirstOrDefault()?.Value?.ToString();

            // Check if the API key is null or empty
            if (string.IsNullOrEmpty(apiKey))
            {
                await command.FollowupAsync("Failed to verify, please try again.", ephemeral: true);
                return;
            }

            // Call the GW2 API to get the account data
            var response = await httpClientFactory.CreateClient().GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");

            // Check if the API call was successful
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Received guild wars 2 response for verifying {guildUserDisplayName}", guildUser.DisplayName);

                // Deserialize the account data
                var stringData = await response.Content.ReadAsStringAsync();
                var accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(stringData) ?? new GuildWars2AccountDataModel();

                // Check if the account already exists in the database
                var isNewAccount = false;
                var account = await databaseContext.Account.FirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);

                if (account != null)
                {
                    // Update existing
                    var gw2Account = databaseContext.GuildWarsAccount.FirstOrDefault(s => s.DiscordId == account.DiscordId && s.GuildWarsAccountId == accountData.Id);
                    if (gw2Account != null)
                    {
                        gw2Account.GuildWarsAccountId = accountData.Id;
                        gw2Account.GuildWarsAccountName = accountData.Name;
                        gw2Account.GuildWarsApiKey = apiKey;
                        gw2Account.FailedApiPullCount = 0;
                        databaseContext.Update(gw2Account);
                    }
                    else
                    {
                        gw2Account = new GuildWarsAccount
                        {
                            GuildWarsAccountId = accountData.Id,
                            DiscordId = account.DiscordId,
                            GuildWarsApiKey = apiKey,
                            GuildWarsAccountName = accountData.Name,
                            GuildWarsGuilds = string.Join(',', accountData.Guilds),
                            World = Convert.ToInt32(accountData.World),
                            FailedApiPullCount = 0
                        };

                        databaseContext.Add(gw2Account);
                    }
                }
                else
                {
                    // Create a new account
                    isNewAccount = true;
                    account = new Account()
                    {
                        DiscordId = (long)command.User.Id
                    };

                    var gw2Account = new GuildWarsAccount
                    {
                        GuildWarsAccountId = accountData.Id,
                        DiscordId = account.DiscordId,
                        GuildWarsApiKey = apiKey,
                        GuildWarsAccountName = accountData.Name,
                        GuildWarsGuilds = string.Join(',', accountData.Guilds),
                        World = Convert.ToInt32(accountData.World),
                        FailedApiPullCount = 0
                    };

                    databaseContext.Add(account);
                    databaseContext.Add(gw2Account);
                }

                await databaseContext.SaveChangesAsync();

                // Get the guild from the database
                var guild = await databaseContext.Guild.FirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);

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
                await AssignRoles(guildUser, guild, inPrimaryGuild, inSecondaryGuild);

                // Edit the response message with the output
                await command.FollowupAsync(output, ephemeral: true);
            }
            else
            {
                logger.LogError("Failed to verify {guildUserDisplayName}", guildUser.DisplayName);

                await command.FollowupAsync($"Looks like you screwed up a couple of letters in the api key, try again mate, failed to process with API key: `{apiKey}`", ephemeral: true);
            }
        }

        public async Task DeverifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            // Check if the account exists in the database
            var output = "";
            var gw2Accounts = await databaseContext.GuildWarsAccount.Where(acc => acc.DiscordId == (long)command.User.Id).ToListAsync();
            var accountFound = gw2Accounts.Any();

            databaseContext.RemoveRange(gw2Accounts);
            await databaseContext.SaveChangesAsync();

            // Generate output message
            output += accountFound ?
                $"Deverify succeeded! Account data cleared for: `{command.User}`" :
                $"Deverify unnecessary! No account data found for: `{command.User}`";

            // Check if the guild exists in the database
            var guilds = await databaseContext.Guild.ToListAsync();
            if (command.GuildId == null) {
                await command.FollowupAsync("Failed to deverify, make sure you asking withing a discord server.", ephemeral: true);
                return;
            }

            var guild = guilds.FirstOrDefault(g => g.GuildId == (long)command.GuildId);

            if (guild == null)
            {
                await command.FollowupAsync(output, ephemeral: true);
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
                logger.LogError(ex, "Failed to de-verify {guildUserDisplayName}", command.User);

                await command.FollowupAsync("Failed to de-verify, please try again.", ephemeral: true);
                return;
            }

            // Remove roles from the user
            await RemoveRoles(guildUser, guild);

            // Modify the response with the output message
            await command.FollowupAsync(output, ephemeral: true);
        }

        private async Task AssignRoles(SocketGuildUser guildUser, Guild guild, bool inPrimaryGuild, bool inSecondaryGuild)
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

        private async Task RemoveRoles(SocketGuildUser guildUser, Guild guild)
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

            IGuildUser user = guildUser;
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