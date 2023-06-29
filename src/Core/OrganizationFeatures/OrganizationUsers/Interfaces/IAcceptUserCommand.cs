using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IAcceptUserCommand
{
    Task<OrganizationUser> AcceptAsync(Guid organizationUserId, User user, string token);

    Task<OrganizationUser> AcceptAsync(string orgIdentifier, User user);

    Task<OrganizationUser> AcceptAsync(Guid organizationId, User user);
}

