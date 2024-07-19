using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubPool
{
    NotificationHubConnection ConnectionFor(Guid comb);
    NotificationHubClient ClientFor(Guid comb);
    INotificationHubProxy AllClients { get; }
}
