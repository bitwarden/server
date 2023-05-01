using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface ISaveOrganizationUserCommand
{
    Task SaveUserAsync(OrganizationUser user, Guid? savingUserId, IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups);
}
