using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

public class CreateScopedOrganizationApiKeyCommand(
    IApiKeyRepository apiKeyRepository,
    IOrganizationRepository organizationRepository,
    IEventService eventService,
    TimeProvider timeProvider)
    : ICreateScopedOrganizationApiKeyCommand
{
    public const int MaxNameLength = 200;
    public const int MaxKeysPerOrganization = 20;
    private const int _clientSecretLength = 30;

    public async Task<CommandResult<CreatedScopedOrganizationApiKey>> CreateAsync(
        CreateScopedOrganizationApiKeyRequest request)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var name = request.Name?.Trim();
        var scopes = request.Scopes?.ToList() ?? [];

        var requestError = ValidateRequest(name, scopes, request.ExpireAt, now);
        if (requestError is not null)
        {
            return requestError;
        }

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization is null)
        {
            return new ScopedApiKeyOrganizationNotFound();
        }

        if (!organization.UseApi)
        {
            return new ScopedApiKeyApiNotAvailable();
        }

        var existingKeys = await apiKeyRepository.GetManyByOrganizationIdAsync(organization.Id);
        if (existingKeys.Count >= MaxKeysPerOrganization)
        {
            return new ScopedApiKeyLimitReached();
        }

        var clientSecret = CoreHelpers.SecureRandomString(_clientSecretLength);
        var apiKey = new ApiKey
        {
            OrganizationId = organization.Id,
            Name = name!,
            ClientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))),
            Scope = JsonSerializer.Serialize(scopes),
            ExpireAt = request.ExpireAt,
            CreationDate = now,
            RevisionDate = now,
        };

        await apiKeyRepository.CreateAsync(apiKey);
        await eventService.LogOrganizationEventAsync(organization, EventType.Organization_ScopedApiKeyCreated);

        return new CreatedScopedOrganizationApiKey(apiKey, clientSecret);
    }

    private static Error? ValidateRequest(string? name, List<string> scopes, DateTime? expireAt, DateTime now)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new ScopedApiKeyNameRequired();
        }

        if (name.Length > MaxNameLength)
        {
            return new ScopedApiKeyNameTooLong();
        }

        if (scopes.Count == 0)
        {
            return new ScopedApiKeyScopesRequired();
        }

        if (scopes.Any(scope => !ApiScopes.OrganizationApiKeyScopes.Contains(scope)))
        {
            return new ScopedApiKeyScopesInvalid();
        }

        if (scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Count)
        {
            return new ScopedApiKeyScopesDuplicated();
        }

        if (expireAt <= now)
        {
            return new ScopedApiKeyExpirationNotInFuture();
        }

        return null;
    }
}
