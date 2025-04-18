namespace Bit.Core.Settings;
public class ServiceBusSettings : ConnectionStringSettings
{
    public string ApplicationCacheTopicName { get; set; }
    public string ApplicationCacheSubscriptionName { get; set; }
    public string WebSiteInstanceId { get; set; }
}

