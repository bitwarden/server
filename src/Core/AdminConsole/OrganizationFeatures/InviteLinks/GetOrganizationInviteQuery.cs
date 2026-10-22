using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Retrieves the opaque invite for an invite link. See
/// <see cref="IGetOrganizationInviteQuery"/> for the behavior.
/// </summary>
/// <remarks>
/// Eligibility, including looking up the invite link, is delegated to <see cref="IGetOrganizationInviteValidator"/>.
/// </remarks>
public class GetOrganizationInviteQuery(IGetOrganizationInviteValidator getOrganizationInviteValidator)
    : IGetOrganizationInviteQuery
{
    public async Task<CommandResult<string>> GetInviteAsync(GetOrganizationInviteRequest request)
    {
        var validationResult = await getOrganizationInviteValidator.ValidateAsync(request);
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        return validationResult.AsSuccess.Invite;
    }
}
