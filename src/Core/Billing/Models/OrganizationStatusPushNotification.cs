// File lives next to its owning domain (Billing) for discoverability; the namespace stays Bit.Core.Models
// so that existing callers do not need updating. A future cleanup can align namespace with file path.
namespace Bit.Core.Models;

public class OrganizationStatusPushNotification
{
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
}
