using System.Security.Claims;
using Bit.Core.Auth.Identity;
using Bit.Core.SecretsManager.Repositories;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

/// <summary>
/// Validates vault_api_key grant type requests for collection-scoped API keys.
/// Allows AI agents and machine clients to authenticate with a client_id + client_secret
/// that is scoped to a specific collection within an organization.
/// </summary>
public class VaultApiKeyGrantValidator : IExtensionGrantValidator
{
    private readonly IApiKeyRepository _apiKeyRepository;

    public VaultApiKeyGrantValidator(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    string IExtensionGrantValidator.GrantType => CustomGrantTypes.VaultApiKey;

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var clientId = context.Request.Raw.Get("client_id");
        var clientSecret = context.Request.Raw.Get("client_secret");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "client_id and client_secret are required.");
            return;
        }

        if (!Guid.TryParse(clientId, out var apiKeyId))
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "client_id must be a valid GUID.");
            return;
        }

        var apiKey = await _apiKeyRepository.GetDetailsByIdAsync(apiKeyId);

        if (apiKey == null)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "Invalid client credentials.");
            return;
        }

        // Verify this is a collection-scoped key (not a service account key)
        if (apiKey.CollectionId == null || apiKey.OrganizationId == null)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "This API key is not collection-scoped. Use the appropriate grant type.");
            return;
        }

        // Check expiration
        if (apiKey.ExpireAt.HasValue && apiKey.ExpireAt.Value < DateTime.UtcNow)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "API key has expired.");
            return;
        }

        // Verify the client secret hash
        var hash = HashSecret(clientSecret);
        if (!string.Equals(hash, apiKey.ClientSecretHash, StringComparison.Ordinal))
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                "Invalid client credentials.");
            return;
        }

        // Build claims for the collection-scoped token
        var claims = new List<Claim>
        {
            new(Claims.Type, IdentityClientType.Organization.ToString()),
            new(Claims.Organization, apiKey.OrganizationId.Value.ToString()),
            new("collection_id", apiKey.CollectionId.Value.ToString()),
            new("scope", string.Join(" ", apiKey.GetScopes())),
        };

        context.Result = new GrantValidationResult(
            subject: apiKey.Id.ToString(),
            authenticationMethod: CustomGrantTypes.VaultApiKey,
            claims: claims);
    }

    private static string HashSecret(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
