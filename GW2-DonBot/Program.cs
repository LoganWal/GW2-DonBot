using Discord;
using Discord.WebSocket;
using DonBot;
using DonBot.Controller.Discord;
using DonBot.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
builder.Services.AddSerilog((_, config) =>
{
    config
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "DonBot-.txt"),
            rollingInterval: RollingInterval.Day)
        .Enrich.FromLogContext()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
});

// Configure service hosting (Windows Service, Linux systemd, or console)
builder.Services.AddWindowsService();
builder.Services.AddSystemd();

// Register application services
ServiceRegister.ConfigureServices(builder.Services);

// Register Discord client
builder.Services.AddSingleton(_ =>
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

builder.Services.AddTransient<IDiscordCore, DiscordCore>();
builder.Services.AddHostedService<DiscordCoreHostedService>();

var host = builder.Build();
host.Run();
