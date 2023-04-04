using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Secrets;

[SutProviderCustomize]
[ProjectCustomize]
public class DeleteSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_Throws_NotFoundException(List<Guid> data,
      SutProvider<DeleteSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(data).Returns(new List<Secret>());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteSecrets(data, default));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().SoftDeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_OneIdNotFound_Throws_NotFoundException(List<Guid> data,
      SutProvider<DeleteSecretCommand> sutProvider)
    {
        var secret = new Secret()
        {
            Id = Guid.NewGuid()
        };
        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(data).Returns(new List<Secret>() { secret });

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteSecrets(data, default));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().SoftDeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task DeleteSecrets_Success(PermissionType permissionType, List<Guid> data,
      SutProvider<DeleteSecretCommand> sutProvider, Guid userId, Guid organizationId, Project mockProject)
    {
        List<Project> projects = null;

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        }
        else
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(mockProject.Id, userId).Returns(true);
            projects = new List<Project>() { mockProject };
        }


        var secrets = new List<Secret>();
        foreach (Guid id in data)
        {
            var secret = new Secret()
            {
                Id = id,
                OrganizationId = organizationId,
                Projects = projects
            };
            secrets.Add(secret);
        }

        sutProvider.GetDependency<ISecretRepository>().GetManyByIds(data).Returns(secrets);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);

        var results = await sutProvider.Sut.DeleteSecrets(data, userId);
        await sutProvider.GetDependency<ISecretRepository>().Received(1).SoftDeleteManyByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));

        foreach (var result in results)
        {
            Assert.Equal("", result.Item2);
        }
    }
}

