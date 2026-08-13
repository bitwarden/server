// File lives next to its owning domain (AdminConsole) for discoverability; the namespace stays
// Bit.Core.Models so that existing callers do not need updating. A future cleanup can align namespace
// with file path.
namespace Bit.Core.Models;

public class AutoConfirmPushNotification
{
    /// <summary>
    /// The admin/owner receiving this notification
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The organization the user accepted an invite to
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The user id who accepted the organization invite (Needed for key-exchange on the client)
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// The organization user id who accepted the organization invite (will be auto-confirmed)
    /// </summary>
    public Guid TargetOrganizationUserId { get; set; }
}
