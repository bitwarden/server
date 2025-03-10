#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IUpdateOrganizationUserCommand
{
    Task UpdateUserAsync(OrganizationUser organizationUser, Guid? savingUserId,
        List<CollectionAccessSelection>? collectionAccess, IEnumerable<Guid>? groupAccess);
}
