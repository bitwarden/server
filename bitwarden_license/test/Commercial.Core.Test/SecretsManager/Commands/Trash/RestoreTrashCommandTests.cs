using Bit.Commercial.Core.SecretsManager.Commands.Trash;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Trash;

[SutProviderCustomize]
[ProjectCustomize]
public class RestoreTrashCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RestoreTrash_Throws_NotFoundException(
        Guid orgId,
        Secret s1,
        Secret s2,
        SutProvider<RestoreTrashCommand> sutProvider
    )
    {
        s1.DeletedDate = DateTime.Now;

        var ids = new List<Guid> { s1.Id, s2.Id };
        sutProvider
            .GetDependency<ISecretRepository>()
            .GetManyByOrganizationIdInTrashByIdsAsync(orgId, ids)
            .Returns(new List<Secret> { s1 });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RestoreTrash(orgId, ids));

        await sutProvider
            .GetDependency<ISecretRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreTrash_Success(
        Guid orgId,
        Secret s1,
        Secret s2,
        SutProvider<RestoreTrashCommand> sutProvider
    )
    {
        s1.DeletedDate = DateTime.Now;

        var ids = new List<Guid> { s1.Id, s2.Id };
        sutProvider
            .GetDependency<ISecretRepository>()
            .GetManyByOrganizationIdInTrashByIdsAsync(orgId, ids)
            .Returns(new List<Secret> { s1, s2 });

        await sutProvider.Sut.RestoreTrash(orgId, ids);

        await sutProvider.GetDependency<ISecretRepository>().Received(1).RestoreManyByIdAsync(ids);
    }
}
