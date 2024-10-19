using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
using Registration;
using Controller.Discord;
using Models.Entities;
using Services.DiscordRequestServices;
using Services.LogGenerationServices;
using Services.Logging;
using Services.PlayerServices;
using Services.SecretsServices;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        logging.AddFilter("Default", LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient();

        // Register DiscordSocketClient
        services.AddSingleton<DiscordSocketClient>(provider =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.DirectMessages |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildWebhooks
            };
            return new DiscordSocketClient(config);
        });

        // Register other services
        ServiceRegister.ConfigureServices(services);

        // Register DiscordCore with the client
        services.AddTransient<IDiscordCore, DiscordCore>(provider =>
        {
            var client = provider.GetRequiredService<DiscordSocketClient>();
            return new DiscordCore(
                provider.GetRequiredService<ILogger<DiscordCore>>(),
                provider.GetRequiredService<ISecretService>(),
                provider.GetRequiredService<IMessageGenerationService>(),
                provider.GetRequiredService<IGenericCommandsService>(),
                provider.GetRequiredService<IVerifyCommandsService>(),
                provider.GetRequiredService<IPointsCommandsService>(),
                provider.GetRequiredService<IRaffleCommandsService>(),
                provider.GetRequiredService<IPollingTasksService>(),
                provider.GetRequiredService<IPlayerService>(),
                provider.GetRequiredService<IRaidService>(),
                provider.GetRequiredService<IDataModelGenerationService>(),
                provider.GetRequiredService<IDiscordCommandService>(),
                provider.GetRequiredService<ILoggingService>(),
                provider.GetRequiredService<IFightLogService>(),
                provider.GetRequiredService<DatabaseContext>(),
                client // Pass the client
            );
        });

        // Register the hosted service
        services.AddHostedService<DiscordCoreHostedService>();
    })
    .Build();

await host.RunAsync();
