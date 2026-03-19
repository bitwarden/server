using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Vault.Commands;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
public class CreateCollectionApiKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_NullCollectionId_ThrowsArgumentException(
        SutProvider<CreateCollectionApiKeyCommand> sutProvider,
        ApiKey apiKey)
    {
        apiKey.CollectionId = null;
        apiKey.OrganizationId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.CreateAsync(apiKey));

        Assert.Contains("CollectionId", exception.Message);

        await sutProvider.GetDependency<IApiKeyRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_NullOrganizationId_ThrowsArgumentException(
        SutProvider<CreateCollectionApiKeyCommand> sutProvider,
        ApiKey apiKey)
    {
        apiKey.CollectionId = Guid.NewGuid();
        apiKey.OrganizationId = null;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.CreateAsync(apiKey));

        Assert.Contains("OrganizationId", exception.Message);

        await sutProvider.GetDependency<IApiKeyRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidInput_GeneratesIdAndSecret(
        SutProvider<CreateCollectionApiKeyCommand> sutProvider,
        ApiKey apiKey)
    {
        apiKey.CollectionId = Guid.NewGuid();
        apiKey.OrganizationId = Guid.NewGuid();
        apiKey.Id = Guid.Empty; // Should be overwritten

        var result = await sutProvider.Sut.CreateAsync(apiKey);

        Assert.NotNull(result);
        Assert.NotNull(result.ClientSecret);
        Assert.NotEmpty(result.ClientSecret);
        Assert.Equal(30, result.ClientSecret.Length);
        Assert.NotEqual(Guid.Empty, result.ApiKey.Id);
        Assert.NotNull(result.ApiKey.ClientSecretHash);
        Assert.NotEmpty(result.ApiKey.ClientSecretHash);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidInput_SetsTimestamps(
        SutProvider<CreateCollectionApiKeyCommand> sutProvider,
        ApiKey apiKey)
    {
        apiKey.CollectionId = Guid.NewGuid();
        apiKey.OrganizationId = Guid.NewGuid();
        var beforeCreate = DateTime.UtcNow;

        var result = await sutProvider.Sut.CreateAsync(apiKey);

        Assert.True(result.ApiKey.CreationDate >= beforeCreate);
        Assert.True(result.ApiKey.RevisionDate >= beforeCreate);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidInput_CallsRepository(
        SutProvider<CreateCollectionApiKeyCommand> sutProvider,
        ApiKey apiKey)
    {
        apiKey.CollectionId = Guid.NewGuid();
        apiKey.OrganizationId = Guid.NewGuid();

        await sutProvider.Sut.CreateAsync(apiKey);

        await sutProvider.GetDependency<IApiKeyRepository>()
            .Received(1)
            .CreateAsync(apiKey);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidInput_HashMatchesSecret(
        SutProvider<CreateCollectionApiKeyCommand> sutProvider,
        ApiKey apiKey)
    {
        apiKey.CollectionId = Guid.NewGuid();
        apiKey.OrganizationId = Guid.NewGuid();

        var result = await sutProvider.Sut.CreateAsync(apiKey);

        // Verify that hashing the returned client secret produces the stored hash
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.ClientSecret);
        var expectedHash = Convert.ToBase64String(sha.ComputeHash(bytes));

        Assert.Equal(expectedHash, result.ApiKey.ClientSecretHash);
    }
}
