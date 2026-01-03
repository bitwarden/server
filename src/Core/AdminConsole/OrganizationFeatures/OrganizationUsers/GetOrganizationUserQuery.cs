using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class GetOrganizationUserQuery(IOrganizationUserRepository organizationUserRepository)
    : IGetOrganizationUserQuery
{
    public async Task<OneOf<InvitedOrganizationUser, AcceptedOrganizationUser, ConfirmedOrganizationUser, None>> GetOrganizationUserAsync(Guid organizationUserId)
    {
        var organizationUser = await organizationUserRepository.GetByIdAsync(organizationUserId);

        if (organizationUser == null)
        {
            return new None();
        }

        // Determine the appropriate model type based on the status
        // For revoked users, use GetPriorActiveOrganizationUserStatusType to determine the underlying status
        var effectiveStatus = organizationUser.Status == OrganizationUserStatusType.Revoked
            ? OrganizationService.GetPriorActiveOrganizationUserStatusType(organizationUser)
            : organizationUser.Status;

        return effectiveStatus switch
        {
            OrganizationUserStatusType.Invited => InvitedOrganizationUser.FromEntity(organizationUser),
            OrganizationUserStatusType.Accepted => AcceptedOrganizationUser.FromEntity(organizationUser),
            OrganizationUserStatusType.Confirmed => ConfirmedOrganizationUser.FromEntity(organizationUser),
            _ => throw new InvalidOperationException($"Unsupported organization user status: {organizationUser.Status}")
        };
    }
}
