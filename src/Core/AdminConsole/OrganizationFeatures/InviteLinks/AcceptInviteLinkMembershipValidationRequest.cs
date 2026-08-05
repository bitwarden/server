using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Request for <see cref="IAcceptInviteLinkMembershipValidator"/>. Carries everything needed to decide
/// whether a user may accept an invite link into <see cref="Organization"/>, including the organization
/// record (so target-org policies can be read directly).
/// </summary>
public record AcceptInviteLinkMembershipValidationRequest
{
    public required Organization Organization { get; init; }
    public required User User { get; init; }
    public required IEnumerable<string> AllowedDomains { get; init; }

    /// <summary>
    /// The user's existing membership in the organization, if any. Non-null only for a pending email
    /// invitation (an <c>Invited</c> row) or a Staged provisioning row. Null for a brand-new member joining
    /// purely via the link.
    /// </summary>
    public OrganizationUser? ExistingMembership { get; init; }

    public string? ResetPasswordKey { get; init; }

    /// <summary>
    /// Whether the Automatic User Confirmation policy is enabled for the organization being joined. Supplied
    /// by the caller (the accept command reads it once and uses it for both validation and post-accept side
    /// effects), keeping it in scope for both without the validator having to hand a result back out.
    /// </summary>
    public bool AutoConfirmPolicyEnabled { get; init; }

    /// <summary>
    /// Whether the joining user must be auto-enrolled in account recovery because the Account Recovery
    /// Administration policy has auto-enrollment enabled. Supplied by the caller, for the same reason as
    /// <see cref="AutoConfirmPolicyEnabled"/>.
    /// </summary>
    public bool AccountRecoveryAutoEnroll { get; init; }
}
