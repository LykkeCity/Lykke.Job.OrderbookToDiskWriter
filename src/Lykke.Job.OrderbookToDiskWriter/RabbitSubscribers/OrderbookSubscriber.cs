using System;
using System.Threading.Tasks;
using System.Net;
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
        private readonly IConsole _console;
        private readonly IDataProcessor _dataProcessor;
        private readonly string _connectionString;
        private readonly string _exchangeName;
        private RabbitMqSubscriber<Orderbook> _subscriber;

        private bool _apiIsReady = false;

        public OrderbookSubscriber(
            ILog log,
            IConsole console,
            IDataProcessor dataProcessor,
            IShutdownManager shutdownManager,
            string connectionString,
            string exchangeName)
        {
            _log = log;
            _console = console;
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
                .SetConsole(_console)
                .Start();
        }

        private async Task ProcessMessageAsync(Orderbook item)
        {
            while (!_apiIsReady)
            {
                WebRequest request = WebRequest.Create($"http://localhost:{Program.Port}/api/isalive");
                try
                {
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    _apiIsReady = response != null && response.StatusCode == HttpStatusCode.OK;
                    if (!_apiIsReady)
                        await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            try
            {
                _dataProcessor.Process(item);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(OrderbookSubscriber), nameof(ProcessMessageAsync), ex);
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
