using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[ProjectCustomize]
public class DeleteSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_Success(SutProvider<DeleteSecretCommand> sutProvider, List<Secret> data)
    {
        await sutProvider.Sut.DeleteSecrets(data);
        await sutProvider.GetDependency<ISecretRepository>()
            .Received(1)
            .SoftDeleteManyByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Select(d => d.Id))));
    }
}

