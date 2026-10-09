using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

/// <summary>
/// A newly created key with its plaintext client secret, which is not stored and can't be retrieved again.
/// </summary>
public record CreatedOrganizationScopedApiKey(OrganizationScopedApiKey ApiKey, string ClientSecret);
