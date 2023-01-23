using Bit.Commercial.Core.SecretManager.Commands.AccessTokens;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManager.AccessTokens;

[SutProviderCustomize]
public class CreateServiceAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CallsCreate(ApiKey data,
      SutProvider<CreateAccessTokenCommand> sutProvider)
    {
        await sutProvider.Sut.CreateAsync(data);

        await sutProvider.GetDependency<IApiKeyRepository>().Received(1)
            .CreateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }
}
