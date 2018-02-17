using System.Threading.Tasks;

namespace Lykke.Job.OrderbookToDiskWriter.Core.Services
{
    public interface IStartupManager
    {
        Task StartAsync();
    }
}