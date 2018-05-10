using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Autofac;
using Lykke.Job.OrderbookToDiskWriter.Core.Services;

namespace Lykke.Job.OrderbookToDiskWriter.Services
{
    public class StartupManager : IStartupManager
    {
        private readonly List<Type> _types = new List<Type>();

        public Task StartAsync(IContainer container)
        {
            foreach (var type in _types)
            {
                var startable = (IStartable)container.Resolve(type);
                startable.Start();
            }

            return Task.CompletedTask;
        }

        public void Register(Type startable)
        {
            _types.Add(startable);
        }
    }
}
