﻿using DonBot.Controller.Discord;
using DonBot.Handlers;
using DonBot.Handlers.GuildWars2Handler.MessageGenerationHandlers;
using DonBot.Models.Entities;
using DonBot.Services.DeadlockServices;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.DiscordServices;
using DonBot.Services.GuildWarsServices;
using DonBot.Services.Logging;
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

            // Services
            services.AddTransient<ISecretService, SecretServices>();
            services.AddTransient<IDataModelGenerationService, DataModelGenerationService>();
            services.AddTransient<IMessageGenerationService, MessageGenerationService>();
            services.AddTransient<IDiscordCore, DiscordCore>();
            services.AddTransient<ILoggingService, LoggingService>();
            services.AddTransient<IPlayerService, PlayerService>();
            services.AddTransient<IRaidCommandService, RaidCommandCommandService>();
            services.AddTransient<IFightLogService, FightLogService>();
            services.AddTransient<IDeadlockApiService, DeadlockApiService>();
            services.AddSingleton<IWordleService, WordleService>();
            services.AddSingleton<IWordGeneratorService, WordGeneratorService>();

            services.AddTransient<IGenericCommandsService, GenericCommandsService>();
            services.AddTransient<IVerifyCommandsService, VerifyCommandsService>();
            services.AddTransient<IPointsCommandsService, PointsCommandsService>();
            services.AddTransient<IRaffleCommandsService, RaffleCommandsService>();
            services.AddTransient<IDiscordCommandService, DiscordCommandService>();
            services.AddTransient<ISteamCommandService, SteamCommandService>();
            services.AddTransient<IDeadlockCommandService, DeadlockCommandService>();

            services.AddTransient<IPollingTasksService, PollingTasksService>();
            services.AddTransient<IDiscordApiService, DiscordApiService>();

            services.AddTransient<SchedulerService>();
            services.AddSingleton<DictionaryService>();

            // DbContext
            services.AddDbContext<DatabaseContext>((serviceProvider, options) =>
            {
                var secretService = serviceProvider.GetRequiredService<ISecretService>();
                var connectionString = secretService.FetchDonBotSqlConnectionString();
                options.UseSqlServer(connectionString);
            });

            // HttpClient
            services.AddHttpClient();
        }
    }
}