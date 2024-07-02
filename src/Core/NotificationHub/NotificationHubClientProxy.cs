using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public class NotificationHubClientProxy : INotificationHubProxy
{
    private readonly IEnumerable<INotificationHubClient> _clients;

    public NotificationHubClientProxy(IEnumerable<INotificationHubClient> clients)
    {
        _clients = clients;
    }

    private async Task ApplyToAllClientsAsync(Func<INotificationHubClient, Task> action)
    {
        var tasks = _clients.Select(async c => await action(c));
        await Task.WhenAll(tasks);
    }

    // partial INotificationHubClient implementation
    // Note: Any other methods that are needed can simply be delegated as done here.
    public async Task DeleteInstallationAsync(string installationId) => await ApplyToAllClientsAsync((c) => c.DeleteInstallationAsync(installationId));
    public async Task DeleteInstallationAsync(string installationId, CancellationToken cancellationToken) => await ApplyToAllClientsAsync(c => c.DeleteInstallationAsync(installationId, cancellationToken));
    public async Task PatchInstallationAsync(string installationId, IList<PartialUpdateOperation> operations) => await ApplyToAllClientsAsync(c => c.PatchInstallationAsync(installationId, operations));
    public async Task PatchInstallationAsync(string installationId, IList<PartialUpdateOperation> operations, CancellationToken cancellationToken) => await ApplyToAllClientsAsync(c => c.PatchInstallationAsync(installationId, operations, cancellationToken));

}
