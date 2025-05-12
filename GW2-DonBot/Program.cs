using Discord;
using Discord.WebSocket;
using DonBot.Controller.Discord;
using DonBot.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace DonBot;

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
                    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "DonBot-.txt"), rollingInterval: RollingInterval.Day)
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
            })
            .ConfigureServices((_, services) =>
            {
                ServiceRegister.ConfigureServices(services);

                services.AddSingleton(_ =>
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

                services.AddTransient<IDiscordCore, DiscordCore>();
                services.AddHostedService<DiscordCoreHostedService>();
            });
}