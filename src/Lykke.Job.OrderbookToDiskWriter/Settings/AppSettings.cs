using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.OrderbookToDiskWriter.Settings
{
    public class AppSettings
    {
        public OrderbookToDiskWriterSettings OrderbookToDiskWriterJob { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
    }

    public class SlackNotificationsSettings
    {
        public AzureQueuePublicationSettings AzureQueue { get; set; }
    }

    public class AzureQueuePublicationSettings
    {
        public string ConnectionString { get; set; }

        public string QueueName { get; set; }
    }

    public class OrderbookToDiskWriterSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }

        public string DiskPath { get; set; }

        public int WarningSizeInGigabytes { get; set; }

        public int MaxSizeInGigabytes { get; set; }

        public RabbitMqSettings Rabbit { get; set; }
    }

    public class RabbitMqSettings
    {
        [AmqpCheck]
        public string ConnectionString { get; set; }

        public string ExchangeName { get; set; }
    }
}
