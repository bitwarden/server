namespace Bit.Core.Settings;

public class EventLoggingSettings
{
    public AzureServiceBusSettings AzureServiceBus { get; set; } = new AzureServiceBusSettings();
    public virtual string WebhookUrl { get; set; }
    public RabbitMqSettings RabbitMq { get; set; } = new RabbitMqSettings();

    public class AzureServiceBusSettings
    {
        private string _connectionString;
        private string _topicName;

        public virtual string EventRepositorySubscriptionName { get; set; } = "events-write-subscription";
        public virtual string WebhookSubscriptionName { get; set; } = "events-webhook-subscription";

        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value.Trim('"');
        }

        public string TopicName
        {
            get => _topicName;
            set => _topicName = value.Trim('"');
        }
    }

    public class RabbitMqSettings
    {
        private string _hostName;
        private string _username;
        private string _password;
        private string _exchangeName;

        public virtual string EventRepositoryQueueName { get; set; } = "events-write-queue";
        public virtual string WebhookQueueName { get; set; } = "events-webhook-queue";

        public string HostName
        {
            get => _hostName;
            set => _hostName = value.Trim('"');
        }
        public string Username
        {
            get => _username;
            set => _username = value.Trim('"');
        }
        public string Password
        {
            get => _password;
            set => _password = value.Trim('"');
        }
        public string ExchangeName
        {
            get => _exchangeName;
            set => _exchangeName = value.Trim('"');
        }
    }
}

