using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Queries;

/// <summary>
/// Counts the ciphers owned by an organization, grouped by cipher type.
/// </summary>
/// <remarks>It does not perform any internal authorization checks.</remarks>
public interface IGetOrganizationCipherTypeCountsQuery
{
    /// <summary>
    /// Returns the number of organization-owned ciphers for each <see cref="CipherType"/> that is currently
    /// available. Available types with no ciphers are included with a count of zero. Types gated behind the
    /// <see cref="FeatureFlagKeys.PM32009_NewItemTypes"/> feature flag are omitted while it is disabled.
    /// Trashed (soft-deleted) items are counted, since they can still be restored.
    /// </summary>
    Task<IReadOnlyDictionary<CipherType, int>> GetCountsAsync(Guid organizationId);
}
