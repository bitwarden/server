using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Enums;

// File lives next to its owning domain (NotificationCenter) for discoverability; the namespace stays
// Bit.Core.Models so that existing callers do not need updating. A future cleanup can align namespace
// with file path.
namespace Bit.Core.Models;

public class NotificationPushNotification
{
    public Guid Id { get; set; }
    public Priority Priority { get; set; }
    public bool Global { get; set; }
    public ClientType ClientType { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? InstallationId { get; set; }
    public Guid? TaskId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime? ReadDate { get; set; }
    public DateTime? DeletedDate { get; set; }
}
