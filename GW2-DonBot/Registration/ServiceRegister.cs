using DonBot.Controller.Discord;
using DonBot.Core.Services.RaidLifecycle;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.DiscordServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Services.LoggingServices;
using DonBot.Services.SchedulerServices;
using DonBot.Services.SecretsServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonBot.Registration;

public static class ServiceRegister
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IFooterService, FooterService>();
        services.AddTransient<IPvEFightSummaryService, PvEFightSummaryService>();
        services.AddTransient<IWvWFightSummaryService, WvWFightSummaryService>();
        services.AddTransient<IRaidReportService, RaidReportService>();
        services.AddTransient<IWeeklyLeaderboardService, WeeklyLeaderboardService>();
        services.AddTransient<IMessageGenerationService, MessageGenerationService>();

        services.AddScoped<ISecretService, SecretServices>();
        services.AddScoped<IDataModelGenerationService, DataModelGenerationService>();
        services.AddScoped<IDiscordCore, DiscordCore>();
        services.AddScoped<DiscordCommandRegistrar>();
        services.AddScoped<DiscordCommandHandler>();
        services.AddScoped<DiscordButtonHandler>();
        services.AddScoped<DiscordMessageHandler>();
        services.AddScoped<ILoggingService, LoggingService>();
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IRaidCommandService, RaidCommandCommandService>();
        services.AddScoped<IRaidLifecycleService, RaidLifecycleService>();
        services.AddScoped<IFightLogService, FightLogService>();
        services.AddScoped<IRotationAnalysisService, RotationAnalysisService>();

        services.AddSingleton<IPendingLogService, PendingLogService>();

        services.AddTransient<IGenericCommandsService, GenericCommandsService>();
        services.AddTransient<IVerifyCommandsService, VerifyCommandsService>();
        services.AddTransient<IPointsCommandsService, PointsCommandsService>();
        services.AddTransient<IRaffleCommandsService, RaffleCommandsService>();
        services.AddTransient<IDiscordCommandService, DiscordCommandService>();
        services.AddTransient<ILeaderboardCommandsService, LeaderboardCommandsService>();

        // Holds a shared rate limiter that must persist across polling cycles.
        services.AddSingleton<IPollingTasksService, PollingTasksService>();
        services.AddTransient<IDiscordApiService, DiscordApiService>();

        services.AddSingleton<SchedulerService>();
        services.AddTransient<IScheduledEventHandler, RaidSignupEventHandler>();
        services.AddTransient<IScheduledEventHandler, WvwRaidSignupEventHandler>();
        services.AddTransient<IScheduledEventHandler, WvwLeaderboardEventHandler>();
        services.AddTransient<IScheduledEventHandler, PveLeaderboardEventHandler>();

        services.AddScoped(typeof(IDatabaseUpdateService<>), typeof(DatabaseUpdateService<>));
        services.AddScoped<IEntityService, EntityService>();

        services.AddDbContextFactory<DatabaseContext>((serviceProvider, options) =>
        {
            var secretService = serviceProvider.GetRequiredService<ISecretService>();
            var connectionString = secretService.FetchDonBotSqlConnectionString();
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("DonBot"));
        });

        services.AddHttpClient();
    }
}
