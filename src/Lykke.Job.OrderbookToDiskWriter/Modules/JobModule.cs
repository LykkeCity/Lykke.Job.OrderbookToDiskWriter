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

        public JobModule(OrderbookToDiskWriterSettings settings, ILog log)
        {
            _settings = settings;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            var startupManager = new StartupManager();
            builder.RegisterInstance(startupManager)
                .As<IStartupManager>()
                .SingleInstance();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>()
                .SingleInstance();

            builder.RegisterResourcesMonitoring(_log);

            builder.RegisterType<DiskWorker>()
                .As<IDiskWorker>()
                .AsSelf()
                //.AutoActivate()
                .SingleInstance();
            startupManager.Register(typeof(DiskWorker));

            builder.RegisterType<DataProcessor>()
                .As<IDataProcessor>()
                .AsSelf()
                //.AutoActivate()
                .SingleInstance()
                .WithParameter(TypedParameter.From(_settings.DiskPath))
                .WithParameter("diskPath", _settings.DiskPath)
                .WithParameter("warningSizeInGigabytes", _settings.WarningSizeInGigabytes)
                .WithParameter("maxSizeInGigabytes", _settings.MaxSizeInGigabytes);
            startupManager.Register(typeof(DataProcessor));

            builder.RegisterType<OrderbookSubscriber>()
                .AsSelf()
                //.AutoActivate()
                .SingleInstance()
                .WithParameter("connectionString", _settings.Rabbit.ConnectionString)
                .WithParameter("exchangeName", _settings.Rabbit.ExchangeName);
            startupManager.Register(typeof(OrderbookSubscriber));
        }
    }
}
