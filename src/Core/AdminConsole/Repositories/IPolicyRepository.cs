using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.AdminConsole.Repositories;

public interface IPolicyRepository : IRepository<Policy, Guid>
{
    /// <summary>
    /// Gets all policies of a given type for an organization.
    /// </summary>
    /// <remarks>
    /// WARNING: do not use this to enforce policies against a user! It returns raw data and does not take into account
    /// various business rules. Use <see cref="IPolicyRequirementQuery"/> instead.
    /// </remarks>
    Task<Policy?> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type);
    Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId);
    Task<IEnumerable<PolicyDetails>> GetPolicyDetailsByUserId(Guid userId);
}
