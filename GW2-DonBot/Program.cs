using Discord;
using Discord.WebSocket;
using DonBot.Controller.Discord;
using DonBot.Models.Entities;
using DonBot.Registration;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.Logging;
using DonBot.Services.SecretsServices;
using DonBot.Services.WordleServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace DonBot
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSerilog((context, config) =>
                {
                    config
                        .ReadFrom.Configuration(context.Configuration)
                        .WriteTo.Console()
                        .WriteTo.File(@"C:\\Logs\\DonBot-.txt", rollingInterval: RollingInterval.Day)
                        .Enrich.FromLogContext()
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning);
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
                                             GatewayIntents.GuildWebhooks |
                                             GatewayIntents.GuildMembers
                        };
                        return new DiscordSocketClient(config);
                    });

                    // Register other services
                    ServiceRegister.ConfigureServices(services);

                    // Register DiscordCore with the client
                    services.AddTransient<IDiscordCore, DiscordCore>(provider => new DiscordCore(
                        provider.GetRequiredService<ILogger<DiscordCore>>(),
                        provider.GetRequiredService<ISecretService>(),
                        provider.GetRequiredService<IMessageGenerationService>(),
                        provider.GetRequiredService<IVerifyCommandsService>(),
                        provider.GetRequiredService<IPointsCommandsService>(),
                        provider.GetRequiredService<IRaffleCommandsService>(),
                        provider.GetRequiredService<IPollingTasksService>(),
                        provider.GetRequiredService<IPlayerService>(),
                        provider.GetRequiredService<IRaidCommandService>(),
                        provider.GetRequiredService<IDataModelGenerationService>(),
                        provider.GetRequiredService<IDiscordCommandService>(),
                        provider.GetRequiredService<ILoggingService>(),
                        provider.GetRequiredService<IFightLogService>(),
                        provider.GetRequiredService<ISteamCommandService>(),
                        provider.GetRequiredService<IDeadlockCommandService>(),
                        provider.GetRequiredService<SchedulerService>(),
                        provider.GetRequiredService<DatabaseContext>(),
                        provider.GetRequiredService<DiscordSocketClient>()
                    ));

                    // Register the hosted service
                    services.AddHostedService<DiscordCoreHostedService>();
                });
    }
}