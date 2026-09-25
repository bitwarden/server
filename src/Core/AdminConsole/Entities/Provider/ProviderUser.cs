using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities.Provider;

/// <summary>
/// Represents a user's membership in a <see cref="Provider"/>, including their role and status.
/// Analogous to <see cref="Bit.Core.Entities.OrganizationUser"/> for organizations.
/// </summary>
public class ProviderUser : ITableObject<Guid>
{
    /// <summary>
    /// A unique identifier for the provider user.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The ID of the <see cref="Provider"/> the user is a member of.
    /// </summary>
    public Guid ProviderId { get; set; }
    /// <summary>
    /// The ID of the <see cref="Bit.Core.Entities.User"/> that is the member. NULL if the Status
    /// is Invited, because the invitation has not yet been linked to a specific user account.
    /// </summary>
    public Guid? UserId { get; set; }
    /// <summary>
    /// The email address of the invited user. NULL once the invitation has been accepted and the
    /// provider user is linked to a <see cref="Bit.Core.Entities.User"/>.
    /// </summary>
    public string? Email { get; set; }
    /// <summary>
    /// The provider's symmetric key, encrypted with the user's symmetric key. NULL if the user
    /// has not yet confirmed their membership.
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// The current status of the provider user, representing their progress in joining the provider.
    /// </summary>
    public ProviderUserStatusType Status { get; set; }
    /// <summary>
    /// The user's role within the provider.
    /// </summary>
    public ProviderUserType Type { get; set; }
    /// <summary>
    /// A JSON blob of permissions for the provider user.
    /// Currently unused as custom permissions are not implemented for providers.
    /// </summary>
    public string? Permissions { get; set; }
    /// <summary>
    /// The date the provider user was created.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the provider user was last updated.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
