using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public class NotificationHubClientProxy : INotificationHubProxy
{
    private readonly IEnumerable<INotificationHubClient> _clients;

    public NotificationHubClientProxy(IEnumerable<INotificationHubClient> clients)
    {
        _clients = clients;
    }

    private async Task<(INotificationHubClient, T)[]> ApplyToAllClientsAsync<T>(
        Func<INotificationHubClient, Task<T>> action
    )
    {
        var tasks = _clients.Select(async c => (c, await action(c)));
        return await Task.WhenAll(tasks);
    }

    // partial proxy of INotificationHubClient implementation
    // Note: Any other methods that are needed can simply be delegated as done here.
    public async Task<(
        INotificationHubClient Client,
        NotificationOutcome Outcome
    )[]> SendTemplateNotificationAsync(IDictionary<string, string> properties, string tagExpression)
    {
        return await ApplyToAllClientsAsync(async c =>
            await c.SendTemplateNotificationAsync(properties, tagExpression)
        );
    }
}
