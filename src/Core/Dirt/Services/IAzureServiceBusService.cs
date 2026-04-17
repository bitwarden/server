using Azure.Messaging.ServiceBus;
using Bit.Core.Dirt.Models.Data.EventIntegrations;

namespace Bit.Core.Dirt.Services;

public interface IAzureServiceBusService : IEventIntegrationPublisher, IAsyncDisposable
{
    ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options);
    Task PublishToRetryAsync(IIntegrationMessage message);
}
