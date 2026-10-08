using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Read-only validation of the policies that gate joining an organization via an invite link.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item>Single Organization: the user's other organizations, then the organization being joined.</item>
///     <item>Require Two-Factor Authentication: the organization being joined.</item>
///     <item>Automatic User Confirmation: the user's other organizations, then the organization being joined.</item>
/// </list>
/// The user's other organizations are evaluated through
/// <see cref="Bit.Core.AdminConsole.OrganizationFeatures.Policies.IPolicyRequirementQuery"/>. The organization being
/// joined is evaluated through
/// <see cref="Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess.IPreAccessPolicyEnforcer"/> with the role
/// the user will hold, because the requirement framework does not return its policies for a brand-new or Staged member.
/// Returns the memberships it read and the Automatic User Confirmation state.
/// </remarks>
public interface IInviteLinkPolicyValidator
{
    Task<CommandResult<InviteLinkPolicyValidationResult>> ValidateAsync(InviteLinkPolicyValidationRequest request);
}
