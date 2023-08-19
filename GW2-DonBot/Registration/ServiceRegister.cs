using Autofac;
using Controller.Discord;
using Services.CacheServices;
using Services.DiscordMessagingServices;
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
        public static void Run()
        {
            RegisterServices().Resolve<ICacheService>();
            RegisterServices().Resolve<ISecretService>();
            RegisterServices().Resolve<IDataModelGenerationService>();
            RegisterServices().Resolve<IMessageGenerationService>();
            RegisterServices().Resolve<ILoggingService>();
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

            return builder.Build();
        }
    }
}