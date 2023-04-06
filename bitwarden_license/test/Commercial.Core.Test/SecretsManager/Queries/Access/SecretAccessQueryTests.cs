using Bit.Commercial.Core.SecretsManager.Queries.Access;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Entities;
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
public class SecretAccessQueryTests
{
    private static void SetupPermission(SutProvider<SecretAccessQuery> sutProvider, PermissionType permissionType, Guid organizationId)
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
    public async Task HasAccess_SecretAccessCheck_AccessSecretsManagerFalse_ReturnsFalse(AccessOperationType accessOperationType, SutProvider<SecretAccessQuery> sutProvider, SecretAccessCheck data)
    {
        data.AccessOperationType = accessOperationType;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .Returns(false);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    public async Task HasAccess_SecretAccessCheck_UpdateSecret_AdminNoProject_ReturnsTrue(PermissionType permissionType, SutProvider<SecretAccessQuery> sutProvider, SecretAccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateSecret;
        data.TargetProjectId = null;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task HasAccess_SecretAccessCheck_UpdateSecret_NotAdminNoProject_ReturnsFalse(PermissionType permissionType, SutProvider<SecretAccessQuery> sutProvider, SecretAccessCheck data)
    {
        data.AccessOperationType = AccessOperationType.UpdateSecret;
        data.TargetProjectId = null;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task HasAccess_SecretAccessCheck_UpdateSecret_ProjectOrgMismatch_ReturnsFalse(PermissionType permissionType, SutProvider<SecretAccessQuery> sutProvider, SecretAccessCheck data, Project mockProject)
    {
        mockProject.Id = data.TargetProjectId!.Value;
        data.AccessOperationType = AccessOperationType.UpdateSecret;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.TargetProjectId.Value)
            .Returns(mockProject);

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, false, true)]
    public async Task HasAccess_SecretAccessCheck_UpdateSecret_ReturnsFalse(PermissionType permissionType, bool currentRead, bool currentWrite, bool read, bool write, SutProvider<SecretAccessQuery> sutProvider, SecretAccessCheck data, Project mockProject)
    {
        mockProject.Id = data.TargetProjectId!.Value;
        mockProject.OrganizationId = data.OrganizationId;
        data.AccessOperationType = AccessOperationType.UpdateSecret;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.TargetProjectId.Value)
            .Returns(mockProject);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(data.CurrentSecretId!.Value, data.UserId, Arg.Any<AccessClientType>())
            .Returns((currentRead, currentWrite));
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(data.TargetProjectId.Value, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task HasAccess_SecretAccessCheck_UpdateSecret_ReturnsTrue(PermissionType permissionType, bool read, bool write, SutProvider<SecretAccessQuery> sutProvider, SecretAccessCheck data, Project mockProject)
    {
        mockProject.Id = data.TargetProjectId!.Value;
        mockProject.OrganizationId = data.OrganizationId;
        data.AccessOperationType = AccessOperationType.UpdateSecret;
        SetupPermission(sutProvider, permissionType, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.TargetProjectId.Value)
            .Returns(mockProject);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(data.CurrentSecretId!.Value, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(data.TargetProjectId.Value, data.UserId, Arg.Any<AccessClientType>())
            .Returns((read, write));

        var result = await sutProvider.Sut.HasAccess(data);

        Assert.True(result);
    }
}
