namespace Lykke.Job.OrderbookToDiskWriter.Core.Services
{
    public interface IDiskWorker
    {
        void AddDataItem(string text, string directoryPath);
    }
}
