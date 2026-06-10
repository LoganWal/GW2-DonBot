using Discord;
using Discord.WebSocket;
using DonBot;
using DonBot.Configuration;
using DonBot.Controller.Discord;
using DonBot.Models.Entities;
using DonBot.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

RuntimeConfiguration.LoadEnvFile();

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddRuntimeConfiguration(args, reloadOnChange: true);

builder.Services.AddSerilog((_, config) => config.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddPortableHostLifetimes();

ServiceRegister.ConfigureServices(builder.Services);

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

await using (var scope = host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    await using var context = await db.CreateDbContextAsync();
    await context.Database.MigrateAsync();
}

host.Run();
