using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DonBotDayOff.Services;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register HttpClient
        services.AddHttpClient(); // This registers IHttpClientFactory and HttpClient

        // Register DiscordSocketClient
        services.AddSingleton<DiscordSocketClient>(provider =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.DirectMessages |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildWebhooks |
                                 GatewayIntents.GuildMembers
            };

            return new DiscordSocketClient(config);
        });

        // Register services
        services.AddSingleton<IWordleService, WordleService>();
        services.AddSingleton<IWordGeneratorService, WordGeneratorService>();
        services.AddSingleton<DictionaryService>();

        // Register SchedulerService with the required dependencies
        services.AddHostedService<SchedulerService>(provider =>
        {
            var wordleService = provider.GetRequiredService<IWordleService>();
            var wordGeneratorService = provider.GetRequiredService<IWordGeneratorService>();
            var logger = provider.GetRequiredService<ILogger<SchedulerService>>();
            var client = provider.GetRequiredService<DiscordSocketClient>();
            var dictionaryService = provider.GetRequiredService<DictionaryService>();

            return new SchedulerService(wordleService, wordGeneratorService, logger, client, dictionaryService);
        });
    })
    .Build();

await host.RunAsync();