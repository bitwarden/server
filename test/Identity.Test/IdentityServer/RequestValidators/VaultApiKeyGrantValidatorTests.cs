using System.Collections.Specialized;
using Bit.Core.Auth.Identity;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Identity.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

[SutProviderCustomize]
public class VaultApiKeyGrantValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_MissingClientId_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        tokenRequest.Raw = new NameValueCollection
        {
            { "client_secret", "some-secret" },
        };

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("client_id and client_secret are required", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MissingClientSecret_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", Guid.NewGuid().ToString() },
        };

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("client_id and client_secret are required", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_InvalidGuidClientId_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", "not-a-guid" },
            { "client_secret", "some-secret" },
        };

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("client_id must be a valid GUID", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ApiKeyNotFound_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", "some-secret" },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns((ApiKeyDetails)null);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("Invalid client credentials", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NotCollectionScoped_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        var apiKey = CreateApiKeyDetails(apiKeyId);
        apiKey.CollectionId = null; // Not collection-scoped

        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", "test-secret" },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns(apiKey);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("not collection-scoped", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ExpiredKey_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        var clientSecret = "test-secret";
        var apiKey = CreateApiKeyDetails(apiKeyId, clientSecret);
        apiKey.ExpireAt = DateTime.UtcNow.AddDays(-1); // Expired yesterday

        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", clientSecret },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns(apiKey);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("expired", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WrongSecret_ReturnsInvalidGrant(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        var apiKey = CreateApiKeyDetails(apiKeyId, "correct-secret");

        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", "wrong-secret" },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns(apiKey);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.True(context.Result.IsError);
        Assert.Contains("Invalid client credentials", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidKey_ReturnsSuccessWithClaims(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var clientSecret = "valid-test-secret";
        var apiKey = CreateApiKeyDetails(apiKeyId, clientSecret, orgId, collectionId);

        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", clientSecret },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns(apiKey);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.False(context.Result.IsError);
        Assert.NotNull(context.Result.Subject);

        var claims = context.Result.Subject.Claims.ToList();
        Assert.Contains(claims, c => c.Type == Claims.Type
            && c.Value == IdentityClientType.Organization.ToString());
        Assert.Contains(claims, c => c.Type == Claims.Organization
            && c.Value == orgId.ToString());
        Assert.Contains(claims, c => c.Type == "collection_id"
            && c.Value == collectionId.ToString());
        Assert.Contains(claims, c => c.Type == "scope");
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NoExpiration_ReturnsSuccess(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        var clientSecret = "no-expiry-secret";
        var apiKey = CreateApiKeyDetails(apiKeyId, clientSecret);
        apiKey.ExpireAt = null; // No expiration

        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", clientSecret },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns(apiKey);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.False(context.Result.IsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_FutureExpiration_ReturnsSuccess(
        [ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<VaultApiKeyGrantValidator> sutProvider)
    {
        var apiKeyId = Guid.NewGuid();
        var clientSecret = "future-expiry-secret";
        var apiKey = CreateApiKeyDetails(apiKeyId, clientSecret);
        apiKey.ExpireAt = DateTime.UtcNow.AddDays(30); // Expires in 30 days

        tokenRequest.Raw = new NameValueCollection
        {
            { "client_id", apiKeyId.ToString() },
            { "client_secret", clientSecret },
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetDetailsByIdAsync(apiKeyId)
            .Returns(apiKey);

        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        await sutProvider.Sut.ValidateAsync(context);

        Assert.False(context.Result.IsError);
    }

    private static CollectionApiKeyDetails CreateApiKeyDetails(
        Guid id,
        string clientSecret = "test-secret",
        Guid? organizationId = null,
        Guid? collectionId = null)
    {
        organizationId ??= Guid.NewGuid();
        collectionId ??= Guid.NewGuid();

        // Use the ApiKey base entity first, then wrap in CollectionApiKeyDetails
        var apiKey = new ApiKey
        {
            Id = id,
            Name = "Test Collection Key",
            OrganizationId = organizationId,
            CollectionId = collectionId,
            ClientSecretHash = HashSecret(clientSecret),
            Scope = "[\"api.vault\"]",
            EncryptedPayload = "encrypted-payload",
            Key = "encryption-key",
            ExpireAt = DateTime.UtcNow.AddDays(90),
        };

        return new CollectionApiKeyDetails(apiKey);
    }

    private static string HashSecret(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
