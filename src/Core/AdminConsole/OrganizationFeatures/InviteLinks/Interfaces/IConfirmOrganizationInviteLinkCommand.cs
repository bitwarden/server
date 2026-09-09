using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Confirms a user into an organization through an invite link. Runs the shared read-only precheck
/// (<see cref="IConfirmOrganizationInviteLinkValidator"/>), creates the membership when the user is not
/// yet a member, confirms it with the supplied organization key, and performs the policy-driven side
/// effects: creating a default collection for Organization Data Ownership and enrolling the user in
/// account recovery when required.
/// </summary>
public interface IConfirmOrganizationInviteLinkCommand
{
    Task<CommandResult> ConfirmAsync(ConfirmOrganizationInviteLinkRequest request);
}
