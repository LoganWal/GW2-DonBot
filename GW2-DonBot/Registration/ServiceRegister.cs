using Autofac;
using Controller.Discord;
using Handlers.MessageGenerationHandlers;
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
        public static IDiscordCore LoadMain()
        {
            return RegisterServices().Resolve<IDiscordCore>();
        }

        private static IContainer RegisterServices()
        {
            var builder = new ContainerBuilder();

            // handlers
            builder.RegisterType<FooterHandler>();
            builder.RegisterType<PvEFightSummaryHandler>();
            builder.RegisterType<WvWFightSummaryHandler>();
            builder.RegisterType<WvWPlayerReportHandler>();
            builder.RegisterType<WvWPlayerSummaryHandler>();
            builder.RegisterType<RaidReportHandler>();

            // services
            builder.RegisterType<CacheService>().As<ICacheService>().SingleInstance();
            builder.RegisterType<SecretServices>().As<ISecretService>();
            builder.RegisterType<DataModelGenerationService>().As<IDataModelGenerationService>();
            builder.RegisterType<MessageGenerationService>().As<IMessageGenerationService>();
            builder.RegisterType<DiscordCore>().As<IDiscordCore>();
            builder.RegisterType<LoggingService>().As<ILoggingService>();
            builder.RegisterType<PlayerService>().As<IPlayerService>();
            builder.RegisterType<RaidService>().As<IRaidService>();

            builder.RegisterType<GenericCommandsService>().As<IGenericCommandsService>();
            builder.RegisterType<VerifyCommandsService>().As<IVerifyCommandsService>();
            builder.RegisterType<PointsCommandsService>().As<IPointsCommandsService>();
            builder.RegisterType<RaffleCommandsService>().As<IRaffleCommandsService>();
            builder.RegisterType<DiscordCommandService>().As<IDiscordCommandService>();
            builder.RegisterType<FightLogService>().As<IFightLogService>();

            builder.RegisterType<PollingTasksService>().As<IPollingTasksService>();
            builder.RegisterType<DiscordApiService>().As<IDiscordApiService>();

            // db
            builder.RegisterType<DatabaseContext>().AsSelf().InstancePerLifetimeScope();

            return builder.Build();
        }
    }
}