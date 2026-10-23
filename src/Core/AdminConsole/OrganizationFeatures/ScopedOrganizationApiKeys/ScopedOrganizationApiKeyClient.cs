namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

/// <summary>
/// The data needed to issue tokens for a usable scoped organization API key.
/// </summary>
/// <param name="AllowedScopes">The key's scopes, limited to the organization API key scope catalog.</param>
public record ScopedOrganizationApiKeyClient(
    Guid OrganizationId,
    Guid KeyId,
    string? ClientSecretHash,
    DateTime? ExpireAt,
    IReadOnlyCollection<string> AllowedScopes);
