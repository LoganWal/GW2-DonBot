using Controller.Discord;
using Handlers.MessageGenerationHandlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.Entities;
using Services.CacheServices;
using Services.DiscordApiServices;
using Services.DiscordRequestServices;
using Services.LogGenerationServices;
using Services.Logging;
using Services.PlayerServices;
using Services.SecretsServices;

namespace Registration
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

            // Services
            services.AddSingleton<ICacheService, CacheService>();
            services.AddTransient<ISecretService, SecretServices>();
            services.AddTransient<IDataModelGenerationService, DataModelGenerationService>();
            services.AddTransient<IMessageGenerationService, MessageGenerationService>();
            services.AddTransient<IDiscordCore, DiscordCore>();
            services.AddTransient<ILoggingService, LoggingService>();
            services.AddTransient<IPlayerService, PlayerService>();
            services.AddTransient<IRaidService, RaidService>();
            services.AddTransient<IFightLogService, FightLogService>();
            services.AddTransient<IDeadlockApiService, DeadlockApiService>();

            services.AddTransient<IGenericCommandsService, GenericCommandsService>();
            services.AddTransient<IVerifyCommandsService, VerifyCommandsService>();
            services.AddTransient<IPointsCommandsService, PointsCommandsService>();
            services.AddTransient<IRaffleCommandsService, RaffleCommandsService>();
            services.AddTransient<IDiscordCommandService, DiscordCommandService>();
            services.AddTransient<ISteamCommandService, SteamCommandService>();
            services.AddTransient<IDeadlockCommandService, DeadlockCommandService>();

            services.AddTransient<IPollingTasksService, PollingTasksService>();
            services.AddTransient<IDiscordApiService, DiscordApiService>();

            // DbContext
            services.AddDbContext<DatabaseContext>((serviceProvider, options) =>
            {
                var secretService = serviceProvider.GetRequiredService<ISecretService>();
                var connectionString = secretService.FetchDonBotSqlConnectionString();
                options.UseSqlServer(connectionString);
            });
        }
    }
}