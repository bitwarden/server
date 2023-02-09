using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Secrets;

[SutProviderCustomize]
[SecretCustomize]
public class CreateSecretCommandTests
{
    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateAsync_Success(PermissionType permissionType, Secret data,
      SutProvider<CreateSecretCommand> sutProvider, Guid userId, Project mockProject)
    {
        data.Projects = new List<Project>() { mockProject };

        if (permissionType == PermissionType.RunAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(true);
        }
        else
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
            sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject((Guid)(data.Projects?.First().Id), userId).Returns(true);
        }

        await sutProvider.Sut.CreateAsync(data, userId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .CreateAsync(data);
    }


    [Theory]
    [BitAutoData]
    public async Task CreateAsync_UserWithoutPermission_ThrowsNotFound(Secret data,
    SutProvider<CreateSecretCommand> sutProvider, Guid userId, Project mockProject)
    {
        data.Projects = new List<Project>() { mockProject };

        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject((Guid)(data.Projects?.First().Id), userId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateAsync(data, userId));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NoProjects_User_ThrowsNotFound(Secret data,
    SutProvider<CreateSecretCommand> sutProvider, Guid userId)
    {
        data.Projects = null;
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateAsync(data, userId));
    }
}

