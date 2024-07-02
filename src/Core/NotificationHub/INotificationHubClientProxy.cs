using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubProxy
{
    Task<(INotificationHubClient client, NotificationOutcome outcome)[]> SendTemplateNotificationAsync(IDictionary<string, string> properties, string tagExpression);
}
