using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubPool
{
    INotificationHubClient ClientFor(Guid comb);
    INotificationHubProxy AllClients { get; }
}
