using System.Threading.Tasks;
using Common;

namespace Lykke.Job.OrderbookToDiskWriter.Core.Services
{
    public interface IShutdownManager
    {
        Task StopAsync();

        void Register(IStopable stopable, int priority);
    }
}
