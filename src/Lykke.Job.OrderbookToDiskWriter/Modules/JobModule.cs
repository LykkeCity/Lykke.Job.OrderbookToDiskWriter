using Autofac;
using Common.Log;
using Lykke.Common;
using Lykke.Job.OrderbookToDiskWriter.Core.Services;
using Lykke.Job.OrderbookToDiskWriter.Settings;
using Lykke.Job.OrderbookToDiskWriter.Services;
using Lykke.Job.OrderbookToDiskWriter.RabbitSubscribers;

namespace Lykke.Job.OrderbookToDiskWriter.Modules
{
    public class JobModule : Module
    {
        private readonly OrderbookToDiskWriterSettings _settings;
        private readonly ILog _log;
        private readonly IConsole _console;

        public JobModule(OrderbookToDiskWriterSettings settings, ILog log, IConsole console)
        {
            _settings = settings;
            _log = log;
            _console = console;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            builder.RegisterInstance(_console)
                .As<IConsole>()
                .SingleInstance();

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            var startupManager = new StartupManager();
            builder.RegisterInstance(startupManager)
                .As<IStartupManager>();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>();

            builder.RegisterResourcesMonitoring(_log);

            builder.RegisterType<DiskWorker>()
                .As<IDiskWorker>()
                .AsSelf()
                //.As<IStartable>()
                //.AutoActivate()
                .SingleInstance();
            startupManager.Register(typeof(DiskWorker));

            builder.RegisterType<DataProcessor>()
                .As<IDataProcessor>()
                .AsSelf()
                //.As<IStartable>()
                //.AutoActivate()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.DiskPath))
                .WithParameter("diskPath", _settings.DiskPath)
                .WithParameter("warningSizeInGigabytes", _settings.WarningSizeInGigabytes)
                .WithParameter("maxSizeInGigabytes", _settings.MaxSizeInGigabytes);
            startupManager.Register(typeof(DataProcessor));

            builder.RegisterType<OrderbookSubscriber>()
                .AsSelf()
                //.As<IStartable>()
                //.AutoActivate()
                .SingleInstance()
                .WithParameter("connectionString", _settings.Rabbit.ConnectionString)
                .WithParameter("exchangeName", _settings.Rabbit.ExchangeName);
            startupManager.Register(typeof(OrderbookSubscriber));
        }
    }
}
