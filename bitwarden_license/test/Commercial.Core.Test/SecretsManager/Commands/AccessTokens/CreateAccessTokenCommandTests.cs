using Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;
using Bit.Core;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Bitwarden.Server.Sdk.Features;
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

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task CreateAsync_ClientSecretPrefixMatchesFlag(bool prefixEnabled,
        SutProvider<CreateAccessTokenCommand> sutProvider, ApiKey data)
    {
        SetPrefixFlagEnabled(sutProvider, prefixEnabled);

        var result = await sutProvider.Sut.CreateAsync(data);

        Assert.Equal(prefixEnabled, result.ClientSecret.StartsWith("bw_"));
    }

    private static void SetPrefixFlagEnabled(SutProvider<CreateAccessTokenCommand> sutProvider, bool enabled)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Sm2093MachineAccountTokenPrefix)
            .Returns(enabled);
    }
}
