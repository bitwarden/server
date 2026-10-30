using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRuleRepository : IRepository<AccessRule, Guid>
{
    Task<ICollection<AccessRule>> GetManyByOrganizationIdAsync(Guid organizationId);

    Task<AccessRuleDetails?> GetDetailsByIdAsync(Guid id);

    Task<ICollection<AccessRuleDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId);
}
