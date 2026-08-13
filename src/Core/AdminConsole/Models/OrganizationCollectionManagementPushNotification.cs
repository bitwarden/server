// File lives next to its owning domain (AdminConsole) for discoverability; the namespace stays
// Bit.Core.Models so that existing callers do not need updating. A future cleanup can align namespace
// with file path.
namespace Bit.Core.Models;

public class OrganizationCollectionManagementPushNotification
{
    public Guid OrganizationId { get; init; }
    public bool LimitCollectionCreation { get; init; }
    public bool LimitCollectionDeletion { get; init; }
    public bool LimitItemDeletion { get; init; }
}
