using DonBot.Controller.Discord;
using DonBot.Handlers;
using DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;
using DonBot.Models.Entities;
using DonBot.Services.DatabaseServices;
using DonBot.Services.DeadlockServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.DiscordServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.LoggingServices;
using DonBot.Services.SecretsServices;
using DonBot.Services.WordleServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DonBot.Registration
{
    public static class ServiceRegister
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            // Handlers
            services.AddTransient<FooterHandler>();
            services.AddTransient<PvEFightSummaryHandler>();
            services.AddTransient<WvWFightSummaryHandler>();
            services.AddTransient<WvWPlayerReportHandler>();
            services.AddTransient<WvWPlayerSummaryHandler>();
            services.AddTransient<RaidReportHandler>();

            // Services (Scoped, Transient, Singleton)
            services.AddScoped<ISecretService, SecretServices>();
            services.AddScoped<IDataModelGenerationService, DataModelGenerationService>();
            services.AddScoped<IMessageGenerationService, MessageGenerationService>();
            services.AddScoped<IDiscordCore, DiscordCore>();
            services.AddScoped<ILoggingService, LoggingService>();
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IRaidCommandService, RaidCommandCommandService>();
            services.AddScoped<IFightLogService, FightLogService>();
            services.AddScoped<IDeadlockApiService, DeadlockApiService>();

            // Singletons (ensure they are stateless)
            services.AddSingleton<IWordleService, WordleService>();
            services.AddSingleton<IWordGeneratorService, WordGeneratorService>();
            services.AddSingleton<DictionaryService>();

            // Command Services
            services.AddTransient<IGenericCommandsService, GenericCommandsService>();
            services.AddTransient<IVerifyCommandsService, VerifyCommandsService>();
            services.AddTransient<IPointsCommandsService, PointsCommandsService>();
            services.AddTransient<IRaffleCommandsService, RaffleCommandsService>();
            services.AddTransient<IDiscordCommandService, DiscordCommandService>();
            services.AddTransient<ISteamCommandService, SteamCommandService>();
            services.AddTransient<IDeadlockCommandService, DeadlockCommandService>();

            // Polling and API Services
            services.AddTransient<IPollingTasksService, PollingTasksService>();
            services.AddTransient<IDiscordApiService, DiscordApiService>();

            // Scheduling
            services.AddTransient<SchedulerService>();

            // DbContext and Unit of Work
            services.AddScoped(typeof(IDatabaseUpdateService<>), typeof(DatabaseUpdateService<>));  // Generic
            services.AddScoped<IEntityService, EntityService>();  // Repository Manager

            // Register IDbContextFactory so it can be injected in your service
            services.AddDbContextFactory<DatabaseContext>((serviceProvider, options) =>
            {
                var secretService = serviceProvider.GetRequiredService<ISecretService>();
                var connectionString = secretService.FetchDonBotSqlConnectionString();
                options.UseSqlServer(connectionString);
            });

            // HttpClient Factory for HTTP requests
            services.AddHttpClient();
        }
    }
}