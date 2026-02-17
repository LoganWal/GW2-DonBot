using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.DiscordRequestServices;

public class DiscordCommandRegistrar(ILogger<DiscordCommandRegistrar> logger)
{
    public async Task RegisterCommands(DiscordSocketClient client)
    {
        await client.BulkOverwriteGlobalApplicationCommandsAsync([]);

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
                .WithDescription("Get your deadlock match history.")
        ];

        var newCommands = commandsToRegister.Select(c => c.Build()).ToArray();

        foreach (var guild in client.Guilds)
        {
            var existingCommands = await guild.GetApplicationCommandsAsync();

            if (existingCommands.Count != newCommands.Length ||
                existingCommands.Any(ec => newCommands.All(nc => nc.Name.Value != ec.Name)))
            {
                logger.LogInformation("Differences found in commands for {GuildName}. Deleting all existing commands", guild.Name);
                await guild.DeleteApplicationCommandsAsync();

                foreach (var command in newCommands)
                {
                    logger.LogInformation("Registering new command on {GuildName} - {CommandName}", guild.Name, command.Name);
                    await guild.CreateApplicationCommandAsync(command);
                }
            }
            else
            {
                logger.LogInformation("No differences in commands for {GuildName}. No action taken", guild.Name);
            }
        }
    }
}
