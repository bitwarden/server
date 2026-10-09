using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

/// <remarks>
/// Implementations encrypt <see cref="OrganizationPartnershipEntitlement.ExternalId"/> at rest, return it
/// decrypted, and derive <see cref="OrganizationPartnershipEntitlement.ExternalIdHash"/> from it on every write.
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

    /// <summary>
    /// Writes <paramref name="entitlement"/> only if the stored row's RevisionDate still equals
    /// <paramref name="expectedRevisionDate"/>, so a change made after it was read is never overwritten.
    /// </summary>
    /// <returns>True if the row was written.</returns>
    Task<bool> ReplaceIfUnchangedAsync(OrganizationPartnershipEntitlement entitlement, DateTime expectedRevisionDate);

    /// <summary>
    /// Clears the held binding only if the entitlement is still canceled, still bound, and its resume window
    /// ended at or before <paramref name="asOf"/>, so a concurrent change to the row is never overwritten.
    /// </summary>
    /// <returns>True if the binding was released.</returns>
    Task<bool> ReleaseExpiredResumeWindowBindingAsync(Guid id, DateTime asOf, DateTime revisionDate);
}
