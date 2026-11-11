using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The result of <see cref="IInviteLinkValidationService.ValidateJoinRequirementsAsync"/>.
/// </summary>
/// <param name="Error">Why the user can't join, or <c>null</c> if allowed.</param>
/// <param name="AutoConfirmPolicyEnabled">If true, the caller must remove the user's emergency access once they join.</param>
public sealed record JoinRequirementsValidationResult(Error? Error, bool AutoConfirmPolicyEnabled)
{
    /// <summary>True if the user can't join.</summary>
    public bool IsError => Error is not null;
}
