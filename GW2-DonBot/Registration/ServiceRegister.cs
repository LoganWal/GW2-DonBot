using Autofac;
using Controller.Discord;
using Models.Entities;
using Services.CacheServices;
using Services.DiscordRequestServices;
using Services.LogGenerationServices;
using Services.Logging;
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

            builder.RegisterType<CacheService>().As<ICacheService>().SingleInstance();
            builder.RegisterType<SecretServices>().As<ISecretService>();
            builder.RegisterType<DataModelGenerationService>().As<IDataModelGenerationService>();
            builder.RegisterType<MessageGenerationService>().As<IMessageGenerationService>();
            builder.RegisterType<DiscordCore>().As<IDiscordCore>();
            builder.RegisterType<LoggingService>().As<ILoggingService>();
            builder.RegisterType<GenericCommands>().As<IGenericCommands>();
            builder.RegisterType<VerifyCommands>().As<IVerifyCommands>();
            builder.RegisterType<PointsCommands>().As<IPointsCommands>();
            builder.RegisterType<RaffleCommands>().As<IRaffleCommands>();
            builder.RegisterType<PollingTasks>().As<IPollingTasks>();
            
            builder.RegisterType<DatabaseContext>().As<IDatabaseContext>();

            return builder.Build();
        }
    }
}