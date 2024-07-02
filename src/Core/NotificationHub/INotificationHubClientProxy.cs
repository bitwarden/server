using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public interface INotificationHubProxy
{
    Task DeleteInstallationAsync(string installationId);
    Task DeleteInstallationAsync(string installationId, CancellationToken cancellationToken);
    Task PatchInstallationAsync(string installationId, IList<PartialUpdateOperation> operations);
    Task PatchInstallationAsync(string installationId, IList<PartialUpdateOperation> operations, CancellationToken cancellationToken);
}
