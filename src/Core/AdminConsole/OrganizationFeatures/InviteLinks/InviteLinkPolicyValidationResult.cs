namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// The policy state produced by <see cref="BaseInviteLinkMembershipValidator"/> when a user passes the join policy
/// checks.
/// </summary>
/// <param name="AutoConfirmPolicyEnabled">The Automatic User Confirmation policy is enforced for the user.</param>
public record InviteLinkPolicyValidationResult(bool AutoConfirmPolicyEnabled);
