using Lykke.Job.OrderbookToDiskWriter.Core.Domain.Models;

namespace Lykke.Job.OrderbookToDiskWriter.Core.Services
{
    public interface IDataProcessor
    {
        void Process(Orderbook item);
    }
}
