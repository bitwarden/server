#nullable enable
using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[SecretCustomize]
[ProjectCustomize]
public class UpdateSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Success(
        SutProvider<UpdateSecretCommand> sutProvider,
        Secret data,
        Project project
    )
    {
        data.Projects = new List<Project> { project };

        await sutProvider.Sut.UpdateAsync(data, null);

        await sutProvider.GetDependency<ISecretRepository>().Received(1).UpdateAsync(data, null);
    }
}
