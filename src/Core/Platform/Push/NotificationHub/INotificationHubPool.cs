using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.Platform.Push.Internal;

public interface INotificationHubPool
{
    NotificationHubConnection ConnectionFor(Guid comb);
    INotificationHubClient ClientFor(Guid comb);
    INotificationHubProxy AllClients { get; }
}
