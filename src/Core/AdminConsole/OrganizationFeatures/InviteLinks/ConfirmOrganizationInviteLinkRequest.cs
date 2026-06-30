using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The data required to confirm a user into an organization via an invite link. The confirmation
/// creates the membership when one does not already exist, releases the organization key to the user,
/// and runs the policy-driven side effects (Organization Data Ownership, account recovery enrollment,
/// and emergency access removal).
/// </summary>
public record ConfirmOrganizationInviteLinkRequest
{
    /// <summary>
    /// The secret code embedded in the invite link the user is confirming with.
    /// </summary>
    public required Guid Code { get; init; }

    /// <summary>
    /// The authenticated user being confirmed into the organization.
    /// </summary>
    public required User User { get; init; }

    /// <summary>
    /// The organization symmetric key encrypted to the user, stored on the membership so the user can
    /// decrypt organization data.
    /// </summary>
    public required string OrgUserKey { get; init; }

    /// <summary>
    /// The user's account recovery (reset password) key, encrypted to the organization's public key.
    /// Required only when the organization enforces automatic account recovery enrollment.
    /// </summary>
    public string? ResetPasswordKey { get; init; }

    /// <summary>
    /// The encrypted name of the default collection to create for the user when the Organization Data
    /// Ownership policy applies.
    /// </summary>
    public required string DefaultUserCollectionName { get; init; }
}
