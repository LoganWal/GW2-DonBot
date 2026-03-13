using DonBot.Controller.Discord;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using DonBot.Services.DeadlockServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.DiscordServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Services.LoggingServices;
using DonBot.Services.SchedulerServices;
using DonBot.Services.SecretsServices;
using DonBot.Services.WordleServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonBot.Registration;

public static class ServiceRegister
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Message Generation Services - Transient for stateless embed generation
        services.AddTransient<IFooterService, FooterService>();
        services.AddTransient<IPvEFightSummaryService, PvEFightSummaryService>();
        services.AddTransient<IWvWFightSummaryService, WvWFightSummaryService>();
        services.AddTransient<IRaidReportService, RaidReportService>();
        services.AddTransient<IWeeklyLeaderboardService, WeeklyLeaderboardService>();
        services.AddTransient<IMessageGenerationService, MessageGenerationService>();

        // Core Services - Scoped for per-request/operation services
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
        services.AddScoped<IFightLogService, FightLogService>();
        services.AddScoped<IDeadlockApiService, DeadlockApiService>();
        services.AddScoped<IRotationAnalysisService, RotationAnalysisService>();

        // Singleton Services - Thread-safe, stateless services
        services.AddSingleton<IWordleService, WordleService>();
        services.AddSingleton<IWordGeneratorService, WordGeneratorService>();
        services.AddSingleton<DictionaryService>();
        services.AddSingleton<IPendingLogService, PendingLogService>();

        // Command Services - Transient for command handlers
        services.AddTransient<IGenericCommandsService, GenericCommandsService>();
        services.AddTransient<IVerifyCommandsService, VerifyCommandsService>();
        services.AddTransient<IPointsCommandsService, PointsCommandsService>();
        services.AddTransient<IRaffleCommandsService, RaffleCommandsService>();
        services.AddTransient<IDiscordCommandService, DiscordCommandService>();
        services.AddTransient<ISteamCommandService, SteamCommandService>();
        services.AddTransient<IDeadlockCommandService, DeadlockCommandService>();
        services.AddTransient<ILeaderboardCommandsService, LeaderboardCommandsService>();

        // Polling and API Services - Transient for operational services
        services.AddTransient<IPollingTasksService, PollingTasksService>();
        services.AddTransient<IDiscordApiService, DiscordApiService>();

        // Scheduling
        services.AddTransient<SchedulerService>();
        services.AddTransient<IScheduledEventHandler, RaidSignupEventHandler>();
        services.AddTransient<IScheduledEventHandler, WvwRaidSignupEventHandler>();
        services.AddTransient<IScheduledEventHandler, WvwLeaderboardEventHandler>();
        services.AddTransient<IScheduledEventHandler, PveLeaderboardEventHandler>();
        services.AddTransient<IScheduledEventHandler, WordleEventHandler>();

        // Database Services
        services.AddScoped(typeof(IDatabaseUpdateService<>), typeof(DatabaseUpdateService<>));
        services.AddScoped<IEntityService, EntityService>();

        // DbContext Factory with connection string resolution
        services.AddDbContextFactory<DatabaseContext>((serviceProvider, options) =>
        {
            var secretService = serviceProvider.GetRequiredService<ISecretService>();
            var connectionString = secretService.FetchDonBotSqlConnectionString();
            options.UseNpgsql(connectionString);
        });

        // HttpClient Factory for HTTP requests with default configuration
        services.AddHttpClient();
    }
}