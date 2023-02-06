using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class DeleteAccessPolicyCommandTests
{
    private static void SetupPermission(SutProvider<DeleteAccessPolicyCommand> sutProvider,
        PermissionType permissionType, Project grantedProject, Guid userId)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(grantedProject.OrganizationId)
                    .Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(grantedProject.Id, userId)
                    .Returns(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    private static void SetupPermission(SutProvider<DeleteAccessPolicyCommand> sutProvider,
        PermissionType permissionType, ServiceAccount grantedServiceAccount, Guid userId)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(grantedServiceAccount.OrganizationId)
                    .Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(grantedServiceAccount.Id, userId)
                    .Returns(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    private static BaseAccessPolicy CreatePolicyToReturn(AccessPolicyType accessPolicyType, Guid data,
        Project grantedProject, Group mockGroup, ServiceAccount mockServiceAccount) =>
        accessPolicyType switch
        {
            AccessPolicyType.UserProjectAccessPolicy => new UserProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = grantedProject.Id,
                GrantedProject = grantedProject,
            },
            AccessPolicyType.GroupProjectAccessPolicy => new GroupProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = grantedProject.Id,
                Group = mockGroup,
                GrantedProject = grantedProject,
            },
            AccessPolicyType.ServiceAccountProjectAccessPolicy => new ServiceAccountProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = grantedProject.Id,
                ServiceAccount = mockServiceAccount,
                GrantedProject = grantedProject,
            },
            _ => null,
        };

    private static BaseAccessPolicy CreatePolicyToReturn(AccessPolicyType accessPolicyType, Guid data,
        ServiceAccount grantedServiceAccount, Group mockGroup) =>
        accessPolicyType switch
        {
            AccessPolicyType.UserServiceAccountAccessPolicy => new UserServiceAccountAccessPolicy
            {
                Id = data,
                GrantedServiceAccountId = grantedServiceAccount.Id,
                GrantedServiceAccount = grantedServiceAccount,
            },
            AccessPolicyType.GroupServiceAccountAccessPolicy => new GroupServiceAccountAccessPolicy
            {
                Id = data,
                GrantedServiceAccountId = grantedServiceAccount.Id,
                Group = mockGroup,
                GrantedServiceAccount = grantedServiceAccount,
            },
            _ => null,
        };

    [Theory]
    [BitAutoData]
    public async Task DeleteAccessPolicy_Throws_NotFoundException(Guid data, Guid userId,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAsync(data, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAccessPolicy_SmNotEnabled_Throws_NotFoundException(Guid data, Guid userId,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(false);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAsync(data, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission)]
    public async Task DeleteAccessPolicy_ProjectGrants_PermissionsCheck_Success(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType,
        Guid data,
        Guid userId,
        Project grantedProject,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        var policyToReturn =
            CreatePolicyToReturn(accessPolicyType, data, grantedProject, mockGroup, mockServiceAccount);
        SetupPermission(sutProvider, permissionType, grantedProject, userId);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await sutProvider.Sut.DeleteAsync(data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).DeleteAsync(Arg.Is(data));
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    public async Task DeleteAccessPolicy_UserProjectAccessPolicy_PermissionsCheck_ThrowsNotAuthorized(
        AccessPolicyType accessPolicyType,
        Guid data,
        Guid userId,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        Project grantedProject,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        var policyToReturn =
            CreatePolicyToReturn(accessPolicyType, data, grantedProject, mockGroup, mockServiceAccount);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteAsync(data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission)]
    public async Task DeleteAccessPolicy_ServiceAccountGrants_PermissionsCheck_Success(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType,
        Guid data,
        Guid userId,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        var policyToReturn = CreatePolicyToReturn(accessPolicyType, data, grantedServiceAccount, mockGroup);
        SetupPermission(sutProvider, permissionType, grantedServiceAccount, userId);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await sutProvider.Sut.DeleteAsync(data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).DeleteAsync(Arg.Is(data));
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task DeleteAccessPolicy_ServiceAccountGrants_PermissionsCheck_Throws(
        AccessPolicyType accessPolicyType,
        Guid data,
        Guid userId,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        var policyToReturn = CreatePolicyToReturn(accessPolicyType, data, grantedServiceAccount, mockGroup);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteAsync(data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }
}
