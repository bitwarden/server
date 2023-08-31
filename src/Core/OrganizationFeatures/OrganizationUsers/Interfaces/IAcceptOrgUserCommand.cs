using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IAcceptOrgUserCommand
{
    Task<OrganizationUser> AcceptOrgUserAsync(Guid organizationUserId, User user, string token, IUserService userService);
    Task<OrganizationUser> AcceptOrgUserAsync(string orgIdentifier, User user, IUserService userService);
    Task<OrganizationUser> AcceptOrgUserAsync(Guid organizationId, User user, IUserService userService);
    Task<OrganizationUser> AcceptOrgUserAsync(OrganizationUser orgUser, User user, IUserService userService);
}
