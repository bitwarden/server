using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

/// <remarks>
/// Implementations encrypt <see cref="OrganizationPartnershipEntitlement.ExternalId"/> at rest and
/// return it decrypted.
/// </remarks>
public interface IOrganizationPartnershipEntitlementRepository
    : IRepository<OrganizationPartnershipEntitlement, Guid>
{
    /// <summary>
    /// Looks up by <see cref="OrganizationPartnershipEntitlement.ComputeExternalIdHash"/>; never by decrypting.
    /// </summary>
    Task<OrganizationPartnershipEntitlement?> GetByExternalIdAsync(Guid organizationPartnershipId, string externalId);

    /// <summary>
    /// Canceled entitlements that still hold a binding and whose resume window ended at or before <paramref name="asOf"/>.
    /// </summary>
    Task<ICollection<OrganizationPartnershipEntitlement>> GetManyCanceledWithExpiredResumeWindowAsync(DateTime asOf);
}
