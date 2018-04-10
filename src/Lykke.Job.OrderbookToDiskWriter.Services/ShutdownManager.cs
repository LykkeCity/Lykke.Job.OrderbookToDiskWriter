using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Common;
using Lykke.Job.OrderbookToDiskWriter.Core.Services;

namespace Lykke.Job.OrderbookToDiskWriter.Services
{
    [UsedImplicitly]
    public class ShutdownManager : IShutdownManager
    {
        private readonly Dictionary<int, List<IStopable>> _items = new Dictionary<int, List<IStopable>>();

        public void Register(IStopable stopable, int priority)
        {
            if (_items.ContainsKey(priority))
                _items[priority].Add(stopable);
            else
                _items.Add(priority, new List<IStopable> { stopable });
        }

        public async Task StopAsync()
        {
            foreach (var priority in _items.Keys.OrderBy(k => k))
            {
                _items[priority].ForEach(s => s.Stop());
            }

            await Task.CompletedTask;
        }
    }
}
