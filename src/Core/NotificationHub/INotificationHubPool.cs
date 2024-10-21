using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubPool
{
    NotificationHubClient ClientFor(Guid comb);
    INotificationHubProxy AllClients { get; }
}
