// File lives next to its owning domain (Vault) for discoverability; the namespace stays Bit.Core.Models
// so that existing callers do not need updating. A future cleanup can align namespace with file path.
namespace Bit.Core.Models;

public class SyncCipherPushNotification
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public IEnumerable<Guid>? CollectionIds { get; set; }
    public DateTime RevisionDate { get; set; }
}
