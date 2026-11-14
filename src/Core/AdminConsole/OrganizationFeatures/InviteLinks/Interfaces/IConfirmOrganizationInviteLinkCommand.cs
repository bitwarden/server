using Bit.Core.AdminConsole.Utilities.v2.Results;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Confirms a user into an organization through an invite link. Runs the shared read-only precheck
/// (<see cref="IConfirmOrganizationInviteLinkValidator"/>), creates the membership when the user is not
/// yet a member, confirms it with the supplied organization key, and performs the policy-driven side
/// effects: creating a default collection for Organization Data Ownership and enrolling the user in
/// account recovery when required. Failures are returned as coded <see cref="InviteLinkValidationError"/>s.
/// </summary>
public interface IConfirmOrganizationInviteLinkCommand
{
    Task<Result<None, InviteLinkValidationError>> ConfirmAsync(ConfirmOrganizationInviteLinkRequest request);
}
