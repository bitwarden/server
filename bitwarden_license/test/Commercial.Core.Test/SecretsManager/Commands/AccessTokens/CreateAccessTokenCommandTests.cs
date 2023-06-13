using Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.AccessTokens;

[SutProviderCustomize]
public class CreateServiceAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NoServiceAccountId_ThrowsBadRequestException(
        SutProvider<CreateAccessTokenCommand> sutProvider, ApiKey data)
    {
        data.ServiceAccountId = null;

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(data));

        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_Success(SutProvider<CreateAccessTokenCommand> sutProvider, ApiKey data)
    {
        await sutProvider.Sut.CreateAsync(data);

        await sutProvider.GetDependency<IApiKeyRepository>().Received(1)
            .CreateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }
}
