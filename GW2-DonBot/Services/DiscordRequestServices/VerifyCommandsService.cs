using Discord;
using Discord.WebSocket;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.DiscordRequestServices;

public sealed class VerifyCommandsService(IEntityService entityService, ILogger<VerifyCommandsService> logger, IHttpClientFactory httpClientFactory) : IVerifyCommandsService
{
    public async Task VerifyCommandExecuted(SocketSlashCommand command, DiscordSocketClient discordClient)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("Failed to verify, please try again.", ephemeral: true);
            return;
        }

        var guildUser = discordClient.GetGuild(command.GuildId.Value).GetUser(command.User.Id);
        var apiKey = command.Data.Options?.FirstOrDefault()?.Value?.ToString();

        if (string.IsNullOrEmpty(apiKey))
        {
            await command.FollowupAsync("Failed to verify, please try again.", ephemeral: true);
            return;
        }

        var response = await httpClientFactory.CreateClient().GetAsync($"https://api.guildwars2.com/v2/account/?access_token={apiKey}");

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Received guild wars 2 response for verifying {guildUserDisplayName}", guildUser.DisplayName);

            var stringData = await response.Content.ReadAsStringAsync();
            var accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(stringData) ?? new GuildWars2AccountDataModel();

            var isNewAccount = false;
            var account = await entityService.Account.GetFirstOrDefaultAsync(a => (ulong)a.DiscordId == command.User.Id);

            if (account != null)
            {
                var gw2Account = await entityService.GuildWarsAccount.GetFirstOrDefaultAsync(s => s.DiscordId == account.DiscordId && s.GuildWarsAccountId == accountData.Id);
                if (gw2Account != null)
                {
                    gw2Account.GuildWarsAccountId = accountData.Id;
                    gw2Account.GuildWarsAccountName = accountData.Name;
                    gw2Account.GuildWarsApiKey = apiKey;
                    gw2Account.FailedApiPullCount = 0;

                    await entityService.GuildWarsAccount.UpdateAsync(gw2Account);
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

                    await entityService.GuildWarsAccount.AddAsync(gw2Account);
                }
            }
            else
            {
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

                await entityService.Account.AddAsync(account);
                await entityService.GuildWarsAccount.AddAsync(gw2Account);
            }

            var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);

            if (guild == null)
            {
                return;
            }

            var output = "";
            output += isNewAccount ?
                $"Verify succeeded! New GW2 account registered: `{accountData.Name}`\n" :
                $"Verify succeeded! GW2 account updated: `{accountData.Name}`\n";

            var rolesConfigured = guild.DiscordVerifiedRoleId != null;
            var primaryGuildId = guild.Gw2GuildMemberRoleId;
            var secondaryGuildIds = guild.Gw2SecondaryMemberRoleIds?.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (rolesConfigured)
            {
                var inPrimaryGuild = primaryGuildId != null && accountData.Guilds.Contains(primaryGuildId);
                var inSecondaryGuild = secondaryGuildIds?.Any(guildId => accountData.Guilds.Contains(guildId)) ?? false;

                var primaryGuildName = primaryGuildId != null
                    ? await GetGw2GuildNameAsync(primaryGuildId)
                    : null;
                var primaryGuildLabel = primaryGuildName != null ? $"`{primaryGuildName}`" : "this server's guild";

                output += inPrimaryGuild ?
                    $"You are in {primaryGuildLabel}, member roles have been assigned! :heart:" :
                    inSecondaryGuild ?
                        "You are in an allied guild, ally roles have been assigned! :heart:" :
                        $"You are not in {primaryGuildLabel} or any allied guild, special roles not assigned. :broken_heart:\nIf you believe this is incorrect, please contact an officer.";

                await AssignRoles(guildUser, guild, inPrimaryGuild, inSecondaryGuild);
            }
            else
            {
                output += "Your account is verified and linked. You can now log in to DonBot.";
            }

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
        var output = "";
        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(acc => acc.DiscordId == (long)command.User.Id);
        var accountFound = gw2Accounts.Any();

        await entityService.GuildWarsAccount.DeleteRangeAsync(gw2Accounts);

        output += accountFound ?
            $"Deverify succeeded! Account data cleared for: `{command.User}`" :
            $"Deverify unnecessary! No account data found for: `{command.User}`";

        var guilds = await entityService.Guild.GetAllAsync();
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

        await RemoveRoles(guildUser, guild);
        await command.FollowupAsync(output, ephemeral: true);
    }

    private async Task<string?> GetGw2GuildNameAsync(string guildId)
    {
        try
        {
            var response = await httpClientFactory.CreateClient().GetAsync($"https://api.guildwars2.com/v2/guild/{guildId}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<GuildWars2GuildDataModel>(json);
            return data?.Name;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch GW2 guild name for {GuildId}", guildId);
            return null;
        }
    }

    private async Task AssignRoles(SocketGuildUser guildUser, Guild guild, bool inPrimaryGuild, bool inSecondaryGuild)
    {
        var primaryRoleId = guild.DiscordGuildMemberRoleId;
        var secondaryRoleId = guild.DiscordSecondaryMemberRoleId;
        var verifiedRoleId = guild.DiscordVerifiedRoleId;

        if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
        {
            await guildUser.SendMessageAsync("Roles are not set up on this server, cannot proceed.");
            return;
        }

        var primaryRole = guildUser.Guild.GetRole((ulong)primaryRoleId);
        var secondaryRole = guildUser.Guild.GetRole((ulong)secondaryRoleId);
        var verifiedRole = guildUser.Guild.GetRole((ulong)verifiedRoleId);

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
        var primaryRoleId = guild.DiscordGuildMemberRoleId;
        var secondaryRoleId = guild.DiscordSecondaryMemberRoleId;
        var verifiedRoleId = guild.DiscordVerifiedRoleId;

        if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
        {
            await guildUser.SendMessageAsync("Roles are not set up on this server, cannot proceed.");
            return;
        }

        IGuildUser user = guildUser;
        var primaryRole = ((IGuildChannel)guildUser.VoiceChannel).Guild.GetRole((ulong)primaryRoleId);
        var secondaryRole = ((IGuildChannel)guildUser.VoiceChannel).Guild.GetRole((ulong)secondaryRoleId);
        var verifiedRole = ((IGuildChannel)guildUser.VoiceChannel).Guild.GetRole((ulong)verifiedRoleId);

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