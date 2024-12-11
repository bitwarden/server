using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[SecretCustomize]
public class CreateSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_Success(
        Secret data,
        SutProvider<CreateSecretCommand> sutProvider,
        Project mockProject
    )
    {
        data.Projects = new List<Project>() { mockProject };

        await sutProvider.Sut.CreateAsync(data, null);

        await sutProvider.GetDependency<ISecretRepository>().Received(1).CreateAsync(data, null);
    }
}
