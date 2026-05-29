using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.ServiceAccounts;

[SutProviderCustomize]
public class RevokeAccessTokenCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RevokeAsyncAsync_Success(ServiceAccount serviceAccount, SutProvider<RevokeAccessTokensCommand> sutProvider)
    {
        var apiKey1 = new ApiKey
        {
            Id = Guid.NewGuid(),
            ServiceAccountId = serviceAccount.Id,
            Name = "Test Name",
            Scope = "Test Scope",
            EncryptedPayload = "Test EncryptedPayload",
            Key = "Test Key",
        };

        var apiKey2 = new ApiKey
        {
            Id = Guid.NewGuid(),
            ServiceAccountId = serviceAccount.Id,
            Name = "Test Name",
            Scope = "Test Scope",
            EncryptedPayload = "Test EncryptedPayload",
            Key = "Test Key",
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetManyByServiceAccountIdAsync(serviceAccount.Id)
            .Returns(new List<ApiKey> { apiKey1, apiKey2 });

        var result = await sutProvider.Sut.RevokeAsync(serviceAccount, new List<Guid> { apiKey1.Id });

        await sutProvider.GetDependency<IApiKeyRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<ApiKey>>(arg => arg.SequenceEqual(new List<ApiKey> { apiKey1 })));
        Assert.Equal(new List<ApiKey> { apiKey1 }, result);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeAsync_NoMatchingTokens_DeletesNothingAndReturnsEmpty(ServiceAccount serviceAccount, SutProvider<RevokeAccessTokensCommand> sutProvider)
    {
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            ServiceAccountId = serviceAccount.Id,
            Name = "Test Name",
            Scope = "Test Scope",
            EncryptedPayload = "Test EncryptedPayload",
            Key = "Test Key",
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetManyByServiceAccountIdAsync(serviceAccount.Id)
            .Returns(new List<ApiKey> { apiKey });

        // Ids that do not belong to this service account's tokens
        var result = await sutProvider.Sut.RevokeAsync(serviceAccount, new List<Guid> { Guid.NewGuid() });

        await sutProvider.GetDependency<IApiKeyRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<ApiKey>>(arg => !arg.Any()));
        Assert.Empty(result);
    }
}
