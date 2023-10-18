using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IUpdateOrganizationUserCommand
{
    Task UpdateUserAsync(OrganizationUser user, Guid? savingUserId, IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups);
}
