using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

#nullable enable

public interface INotificationHubProxy
{
    Task<(INotificationHubClient Client, NotificationOutcome Outcome)[]> SendTemplateNotificationAsync(IDictionary<string, string> properties, string tagExpression);
}
