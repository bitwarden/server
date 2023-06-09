using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IInviteOrganizationUserCommand
{
    Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId, IEnumerable<(OrganizationUserInvite invite, string externalId)> invites);
    Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, EventSystemUser systemUser, IEnumerable<(OrganizationUserInvite invite, string externalId)> invites);
    Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, string email, OrganizationUserType type, bool accessAll, string externalId, IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups);
    Task<OrganizationUser> InviteUserAsync(Guid organizationId, EventSystemUser systemUser, string email, OrganizationUserType type, bool accessAll, string externalId, IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups);
}
