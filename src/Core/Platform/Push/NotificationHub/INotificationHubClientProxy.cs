using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.Platform.Push.Internal;

public interface INotificationHubProxy
{
    Task<(INotificationHubClient Client, NotificationOutcome Outcome)[]> SendTemplateNotificationAsync(IDictionary<string, string> properties, string tagExpression);
}
