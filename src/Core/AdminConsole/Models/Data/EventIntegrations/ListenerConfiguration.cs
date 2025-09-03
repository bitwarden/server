using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public abstract class ListenerConfiguration
{
    protected GlobalSettings _globalSettings;

    public ListenerConfiguration(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public int MaxRetries
    {
        get => _globalSettings.EventLogging.MaxRetries;
    }

    public string EventTopicName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.EventTopicName;
    }

    public string IntegrationTopicName
    {
        get => _globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName;
    }
}
