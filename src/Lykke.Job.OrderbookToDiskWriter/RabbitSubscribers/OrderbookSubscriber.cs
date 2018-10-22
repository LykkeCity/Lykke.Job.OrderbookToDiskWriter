using System;
using System.Threading.Tasks;
using Autofac;
using Common;
using Common.Log;
using Lykke.Job.OrderbookToDiskWriter.Core.Domain.Models;
using Lykke.Job.OrderbookToDiskWriter.Core.Services;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;

namespace Lykke.Job.OrderbookToDiskWriter.RabbitSubscribers
{
    public class OrderbookSubscriber : IStartable, IStopable
    {
        private readonly ILog _log;
        private readonly IDataProcessor _dataProcessor;
        private readonly string _connectionString;
        private readonly string _exchangeName;
        private RabbitMqSubscriber<Orderbook> _subscriber;

        public OrderbookSubscriber(
            ILog log,
            IDataProcessor dataProcessor,
            IShutdownManager shutdownManager,
            string connectionString,
            string exchangeName)
        {
            _log = log;
            _dataProcessor = dataProcessor;
            _connectionString = connectionString;
            _exchangeName = exchangeName;

            shutdownManager.Register(this, 0);
        }

        public void Start()
        {
            var settings = RabbitMqSubscriptionSettings
                .CreateForSubscriber(_connectionString, _exchangeName, "orderbooktodiskwriter")
                .MakeDurable();

            _subscriber = new RabbitMqSubscriber<Orderbook>(settings,
                    new ResilientErrorHandlingStrategy(_log, settings,
                        retryTimeout: TimeSpan.FromSeconds(10),
                        next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                .SetMessageDeserializer(new JsonMessageDeserializer<Orderbook>())
                .Subscribe(ProcessMessageAsync)
                .CreateDefaultBinding()
                .SetLogger(_log)
                .Start();
        }

        private Task ProcessMessageAsync(Orderbook item)
        {
            try
            {
                _dataProcessor.Process(item);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _log.WriteError(nameof(OrderbookSubscriber), nameof(ProcessMessageAsync), ex);
                throw;
            }
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }

        public void Stop()
        {
            _subscriber?.Stop();
        }
    }
}
