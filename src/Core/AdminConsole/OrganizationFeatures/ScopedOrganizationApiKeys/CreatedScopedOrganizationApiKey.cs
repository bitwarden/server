using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

/// <summary>
/// A newly created key with its plaintext client secret, which is not stored and can't be retrieved again.
/// </summary>
public record CreatedScopedOrganizationApiKey(ApiKey ApiKey, string ClientSecret);
