using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubPool
{
    NotificationHubClient ClientFor(Guid comb, ApplicationChannel channel);
    INotificationHubProxy AllClients { get; }
}
