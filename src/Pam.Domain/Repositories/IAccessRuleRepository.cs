using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRuleRepository : IRepository<AccessRule, Guid>
{
    Task<ICollection<AccessRule>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the access rule along with the IDs of the collections it governs, or null if it does not exist.
    /// </summary>
    Task<AccessRuleDetails?> GetDetailsByIdAsync(Guid id);

    /// <summary>
    /// Returns all access rules in the organization, each along with the IDs of the collections it governs.
    /// </summary>
    Task<ICollection<AccessRuleDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId);
}
