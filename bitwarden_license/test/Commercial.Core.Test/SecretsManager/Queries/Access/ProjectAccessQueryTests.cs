using Bit.Commercial.Core.SecretsManager.Queries.Access;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;


namespace Bit.Commercial.Core.Test.SecretsManager.Queries.Access;

[SutProviderCustomize]
[ProjectCustomize]
public class ProjectAccessQueryTests
{
    private static void SetupPermission(SutProvider<ProjectAccessQuery> sutProvider, PermissionType permissionType, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessToCreate_AccessSecretsManagerFalse_ReturnsFalse(SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(false);

        var result = await sutProvider.Sut.HasAccessToCreateAsync(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task HasAccessToCreate_ShouldReturnFalse(ClientType clientType, SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);

        var result = await sutProvider.Sut.HasAccessToCreateAsync(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task HasAccessToCreate_ShouldReturnTrue(PermissionType permissionType, SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);

        var result = await sutProvider.Sut.HasAccessToCreateAsync(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessToUpdate_AccessSecretsManagerFalse_ReturnsFalse(SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(false);

        var result = await sutProvider.Sut.HasAccessToUpdateAsync(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccessToUpdateAsync_SaAccessClient_ShouldReturnFalse(SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.ServiceAccount);

        var result = await sutProvider.Sut.HasAccessToUpdateAsync(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task HasAccessToUpdate_ShouldReturnFalse(PermissionType permissionType, bool read, bool write, SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccessToUpdateAsync(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task HasAccessToUpdateAsync_ShouldReturnTrue(PermissionType permissionType, bool read, bool write, SutProvider<ProjectAccessQuery> sutProvider, AccessCheck data)
    {
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccessToUpdateAsync(data);

        Assert.True(result);
    }
}
