using Bit.Core.AdminConsole.Entities;

// File lives next to its owning domain (AdminConsole) for discoverability; the namespace stays
// Bit.Core.Models so that existing callers do not need updating. A future cleanup can align namespace
// with file path.
namespace Bit.Core.Models;

public class SyncPolicyPushNotification
{
    public Guid OrganizationId { get; set; }
    public required Policy Policy { get; set; }
}
