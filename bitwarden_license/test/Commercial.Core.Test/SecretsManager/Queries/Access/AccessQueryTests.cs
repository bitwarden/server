using Bit.Commercial.Core.SecretsManager.Queries.Access;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Enums;
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
public class AccessQueryTests
{
    private static void SetupPermission(SutProvider<AccessQuery> sutProvider, PermissionType permissionType, Guid organizationId)
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
    [BitAutoData(AccessOperationType.CreateAccessToken)]
    [BitAutoData(AccessOperationType.RevokeAccessToken)]
    [BitAutoData(AccessOperationType.CreateServiceAccount)]
    [BitAutoData(AccessOperationType.UpdateServiceAccount)]
    [BitAutoData(AccessOperationType.CreateProject)]
    [BitAutoData(AccessOperationType.UpdateProject)]
    [BitAutoData(AccessOperationType.CreateSecret)]
    [BitAutoData(AccessOperationType.UpdateSecret)]
    public async Task HasAccess_AccessSecretsManagerFalse_ReturnsFalse(AccessOperationType accessOperationType, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = accessOperationType;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(false);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task HasAccess_RevokeAccessToken_ShouldReturnFalse(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.RevokeAccessToken;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task HasAccess_RevokeAccessToken_ShouldReturnTrue(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.RevokeAccessToken;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task HasAccess_CreateAccessToken_ShouldReturnFalse(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.CreateAccessToken;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task HasAccess_CreateAccessToken_ShouldReturnTrue(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.CreateAccessToken;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task HasAccess_CreateServiceAccount_ShouldReturnFalse(ClientType clientType, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.CreateServiceAccount;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task HasAccess_CreateServiceAccount_ShouldReturnTrue(PermissionType permissionType, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.CreateServiceAccount;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task HasAccess_UpdateServiceAccount_ShouldReturnFalse(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateServiceAccount;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task HasAccess_UpdateServiceAccount_ShouldReturnTrue(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateServiceAccount;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task HasAccess_CreateProject_ShouldReturnFalse(ClientType clientType, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.CreateProject;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task HasAccess_CreateProject_ShouldReturnTrue(PermissionType permissionType, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.CreateProject;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task HasAccess_UpdateProject_SaAccessClient_ShouldReturnFalse(SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateProject;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.ServiceAccount);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task HasAccess_UpdateProject_ShouldReturnFalse(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateProject;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task HasAccess_UpdateProject_ShouldReturnTrue(PermissionType permissionType, bool read, bool write, SutProvider<AccessQuery> sutProvider, AccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateProject;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(data.TargetId, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }
}
