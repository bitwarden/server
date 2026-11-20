using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Checks that a user can accept an organization invite link, in this order:
/// <list type="bullet">
///     <item>The invite link and organization are eligible.</item>
///     <item>The user's existing membership allows accepting.</item>
///     <item>The free organization admin limit.</item>
///     <item>The Automatic User Confirmation policy.</item>
///     <item>The Single Organization policy.</item>
///     <item>The Require Two-Factor Authentication policy.</item>
///     <item>The Account Recovery key requirement.</item>
/// </list>
/// </summary>
public interface IAcceptInviteLinkMembershipValidator
{
    Task<Result<AcceptInviteLinkMembershipValidationResult, InviteLinkValidationError>> ValidateAsync(
        AcceptInviteLinkMembershipValidationRequest request);
}
