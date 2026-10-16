namespace Bit.Core.AdminConsole.Context;

/// <summary>
/// Identifies whether the current request is authenticated with a scoped organization API key.
/// </summary>
public interface IScopedOrganizationApiKeyContext
{
    /// <summary>
    /// True when the request's client id has the shape organization.{organizationId}.{keyId}; false outside an HTTP request.
    /// </summary>
    bool IsScopedOrganizationApiKey { get; }
}
