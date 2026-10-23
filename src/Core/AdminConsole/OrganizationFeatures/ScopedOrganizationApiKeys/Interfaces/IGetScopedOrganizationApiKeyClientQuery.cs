namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;

public interface IGetScopedOrganizationApiKeyClientQuery
{
    /// <summary>
    /// Returns the key's client data, or null unless the key belongs to the organization, is unexpired,
    /// and the organization is enabled and can use the API.
    /// </summary>
    Task<ScopedOrganizationApiKeyClient?> GetAsync(Guid organizationId, Guid keyId);
}
