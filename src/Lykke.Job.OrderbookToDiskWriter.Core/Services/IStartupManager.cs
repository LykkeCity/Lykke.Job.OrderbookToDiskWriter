using System.Threading.Tasks;
using Autofac;

namespace Lykke.Job.OrderbookToDiskWriter.Core.Services
{
    public interface IStartupManager
    {
        Task StartAsync(IContainer container);
    }
}
