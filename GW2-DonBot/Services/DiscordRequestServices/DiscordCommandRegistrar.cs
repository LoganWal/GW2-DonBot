using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordRequestServices;

public class DiscordCommandRegistrar(ILogger<DiscordCommandRegistrar> logger)
{
    public async Task RegisterCommands(DiscordSocketClient client)
    {
        var globalCommands = await client.GetGlobalApplicationCommandsAsync();
        if (globalCommands.Count > 0)
        {
            logger.LogInformation("Clearing {Count} global command(s)", globalCommands.Count);
            await client.BulkOverwriteGlobalApplicationCommandsAsync([]);
        }

        var newCommands = BuildCommands();

        foreach (var guild in client.Guilds)
        {
            await RegisterCommandsForGuild(guild, newCommands);
        }
    }

    public async Task RegisterCommandsForGuild(SocketGuild guild)
    {
        await RegisterCommandsForGuild(guild, BuildCommands());
    }

    private static ApplicationCommandProperties[] BuildCommands()
    {
        SlashCommandBuilder[] commandsToRegister =
        [
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

            new SlashCommandBuilder()
                .WithName("gw2_server_config")
                .WithDescription("Configure DonBot settings for this server.")
                .WithDefaultMemberPermissions(GuildPermission.Administrator)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("log_drop_off_channel")
                    .WithDescription("Set the channel where fight logs are dropped off.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("guild_member_role")
                    .WithDescription("Set the Discord role assigned to GW2 guild members.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("role", ApplicationCommandOptionType.Role, "The role", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("secondary_member_role")
                    .WithDescription("Set the Discord role assigned to secondary guild members.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("role", ApplicationCommandOptionType.Role, "The role", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("verified_role")
                    .WithDescription("Set the Discord role assigned to verified members.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("role", ApplicationCommandOptionType.Role, "The role", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("gw2_guild_member_role_id")
                    .WithDescription("Set the GW2 guild ID used to assign the member role.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.String, "The GW2 guild ID (UUID)", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("gw2_secondary_member_role_ids")
                    .WithDescription("Set the GW2 guild IDs used to assign the secondary member role (comma-separated).")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.String, "Comma-separated GW2 guild IDs", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("player_report_channel")
                    .WithDescription("Set the channel for player reports.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("wvw_activity_report_channel")
                    .WithDescription("Set the channel for WvW player activity reports.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("announcement_channel")
                    .WithDescription("Set the channel for announcements.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("log_report_channel")
                    .WithDescription("Set the channel for log reports.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("advance_log_report_channel")
                    .WithDescription("Set the channel for advanced log reports.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("stream_log_channel")
                    .WithDescription("Set the channel for stream log output.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("raid_alert_enabled")
                    .WithDescription("Enable or disable raid alerts.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.Boolean, "Enable raid alerts?", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("raid_alert_channel")
                    .WithDescription("Set the channel for raid alerts.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("remove_spam_enabled")
                    .WithDescription("Enable or disable automatic spam removal.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.Boolean, "Enable spam removal?", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("removed_message_channel")
                    .WithDescription("Set the channel where removed spam messages are logged.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("channel", ApplicationCommandOptionType.Channel, "The channel", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("auto_submit_to_wingman")
                    .WithDescription("Automatically submit dps.report logs to gw2wingman for import. Default: enabled.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.Boolean, "Enable auto wingman submission?", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("auto_aggregate_logs")
                    .WithDescription("Automatically post an aggregate summary when multiple logs are shared at once. Default: enabled.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.Boolean, "Enable auto log aggregation?", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("auto_reply_single_log")
                    .WithDescription("Automatically reply with a fight summary when a single log is shared. Default: disabled.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.Boolean, "Enable single log auto-reply?", isRequired: true))
        ];

        return commandsToRegister.Select(c => (ApplicationCommandProperties)c.Build()).ToArray();
    }

    private async Task RegisterCommandsForGuild(SocketGuild guild, ApplicationCommandProperties[] newCommands)
    {
        var existingCommands = await guild.GetApplicationCommandsAsync();

        var commandsChanged = existingCommands.Count != newCommands.Length ||
            existingCommands.Any(ec => newCommands.All(nc => nc.Name.Value != ec.Name)) ||
            existingCommands.Any(ec =>
            {
                var matching = newCommands.FirstOrDefault(nc => nc.Name.Value == ec.Name);
                return matching is SlashCommandProperties sc &&
                       (sc.Options.IsSpecified ? sc.Options.Value.Count : 0) != ec.Options.Count;
            });

        if (commandsChanged)
        {
            logger.LogInformation("Differences found in commands for {GuildName}. Overwriting commands", guild.Name);
            await ((IGuild)guild).BulkOverwriteApplicationCommandsAsync(newCommands);
        }
        else
        {
            logger.LogInformation("No differences in commands for {GuildName}. No action taken", guild.Name);
        }
    }
}
