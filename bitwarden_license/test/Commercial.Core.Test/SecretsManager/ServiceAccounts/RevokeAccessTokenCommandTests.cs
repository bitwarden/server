using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.ServiceAccounts;

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
            ServiceAccountId = serviceAccount.Id
        };

        var apiKey2 = new ApiKey
        {
            Id = Guid.NewGuid(),
            ServiceAccountId = serviceAccount.Id
        };

        sutProvider.GetDependency<IApiKeyRepository>()
            .GetManyByServiceAccountIdAsync(serviceAccount.Id)
            .Returns(new List<ApiKey> { apiKey1, apiKey2 });

        await sutProvider.Sut.RevokeAsync(serviceAccount, new List<Guid> { apiKey1.Id });

        await sutProvider.GetDependency<IApiKeyRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<ApiKey>>(arg => arg.SequenceEqual(new List<ApiKey> { apiKey1 })));
    }
}
