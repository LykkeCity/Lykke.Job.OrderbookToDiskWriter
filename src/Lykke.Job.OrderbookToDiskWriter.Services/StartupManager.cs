using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using Autofac;
using Common.Log;
using Lykke.Job.OrderbookToDiskWriter.Core;
using Lykke.Job.OrderbookToDiskWriter.Core.Services;

namespace Lykke.Job.OrderbookToDiskWriter.Services
{
    public class StartupManager : IStartupManager
    {
        private readonly ILog _log;
        private readonly List<Type> _types = new List<Type>();

        private bool _apiIsReady = false;

        public StartupManager(ILog log)
        {
            _log = log;
        }

        public async Task StartAsync(IContainer container)
        {
            while (!_apiIsReady)
            {
                WebRequest request = WebRequest.Create($"http://localhost:{Constants.Port}/api/isalive");
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

            foreach (var type in _types)
            {
                var startable = (IStartable)container.Resolve(type);
                startable.Start();
            }
        }

        public void Register(Type startable)
        {
            _types.Add(startable);
        }
    }
}
