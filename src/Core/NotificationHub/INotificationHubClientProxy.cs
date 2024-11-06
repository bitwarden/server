using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubProxy
{
    Task<(INotificationHubClient Client, NotificationOutcome Outcome)[]> SendTemplateNotificationAsync(IDictionary<string, string> properties, string tagExpression);
}
