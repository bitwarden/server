using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Performs the read-only validation and policy precheck for accepting an organization invite link. No write
/// operations are performed; the caller is responsible for any state changes once validation succeeds.
/// </summary>
/// <remarks>
/// See <see cref="BaseInviteLinkMembershipValidator"/> for the checks performed. An existing membership must be
/// neither revoked nor already accepted or confirmed.
/// </remarks>
public interface IAcceptInviteLinkMembershipValidator
{
    Task<CommandResult<InviteLinkMembershipValidationResult>> ValidateAsync(
        AcceptOrganizationInviteLinkRequest request);
}
