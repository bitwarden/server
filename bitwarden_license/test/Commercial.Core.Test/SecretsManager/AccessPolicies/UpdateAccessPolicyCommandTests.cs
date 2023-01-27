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
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class UpdateAccessPolicyCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Throws_NotFoundException(Guid data, bool read, bool write, Guid userId,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data, read, write, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission)]
    public async Task UpdateAsync_ProjectGrants_PermissionsCheck_Success(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        Project grantedProject,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserProjectAccessPolicy:
                policyToReturn =
                    new UserProjectAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedProjectId = grantedProject.Id,
                    };
                break;
            case AccessPolicyType.GroupProjectAccessPolicy:
                mockGroup.OrganizationId = grantedProject.OrganizationId;
                policyToReturn =
                    new GroupProjectAccessPolicy
                    {
                        Id = data,
                        GrantedProjectId = grantedProject.Id,
                        Read = true,
                        Write = true,
                        Group = mockGroup,
                    };
                break;
            case AccessPolicyType.ServiceAccountProjectAccessPolicy:
                mockServiceAccount.OrganizationId = grantedProject.OrganizationId;
                policyToReturn = new ServiceAccountProjectAccessPolicy
                {
                    Id = data,
                    GrantedProjectId = grantedProject.Id,
                    Read = true,
                    Write = true,
                    ServiceAccount = mockServiceAccount,
                };
                break;
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(grantedProject.Id).Returns(grantedProject);
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
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);
        var result = await sutProvider.Sut.UpdateAsync(data, read, write, userId);
        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).ReplaceAsync(policyToReturn);

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    public async Task UpdateAsync_ProjectGrants_PermissionsCheck_Throws(
        AccessPolicyType accessPolicyType,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        Project grantedProject,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserProjectAccessPolicy:
                policyToReturn =
                    new UserProjectAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedProjectId = grantedProject.Id,
                    };
                break;
            case AccessPolicyType.GroupProjectAccessPolicy:
                mockGroup.OrganizationId = grantedProject.OrganizationId;
                policyToReturn =
                    new GroupProjectAccessPolicy
                    {
                        Id = data,
                        GrantedProjectId = grantedProject.Id,
                        Read = true,
                        Write = true,
                        Group = mockGroup,
                    };
                break;
            case AccessPolicyType.ServiceAccountProjectAccessPolicy:
                mockServiceAccount.OrganizationId = grantedProject.OrganizationId;
                policyToReturn = new ServiceAccountProjectAccessPolicy
                {
                    Id = data,
                    GrantedProjectId = grantedProject.Id,
                    Read = true,
                    Write = true,
                    ServiceAccount = mockServiceAccount,
                };
                break;
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(grantedProject.Id).Returns(grantedProject);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateAsync(data, read, write, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsAdmin)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission)]
    public async Task UpdateAsync_ServiceAccountGrants_PermissionsCheck_Success(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserServiceAccountAccessPolicy:
                policyToReturn =
                    new UserServiceAccountAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                    };
                break;
            case AccessPolicyType.GroupServiceAccountAccessPolicy:
                mockGroup.OrganizationId = grantedServiceAccount.OrganizationId;
                policyToReturn =
                    new GroupServiceAccountAccessPolicy
                    {
                        Id = data,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                        Read = true,
                        Write = true,
                        Group = mockGroup,
                    };
                break;
        }

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(grantedServiceAccount.Id)
            .Returns(grantedServiceAccount);
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
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);
        var result = await sutProvider.Sut.UpdateAsync(data, read, write, userId);
        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).ReplaceAsync(policyToReturn);

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task UpdateAsync_ServiceAccountGrants_PermissionsCheck_Throws(
        AccessPolicyType accessPolicyType,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserServiceAccountAccessPolicy:
                policyToReturn =
                    new UserServiceAccountAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                    };
                break;
            case AccessPolicyType.GroupServiceAccountAccessPolicy:
                mockGroup.OrganizationId = grantedServiceAccount.OrganizationId;
                policyToReturn =
                    new GroupServiceAccountAccessPolicy
                    {
                        Id = data,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                        Read = true,
                        Write = true,
                        Group = mockGroup,
                    };
                break;
        }

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(grantedServiceAccount.Id)
            .Returns(grantedServiceAccount);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateAsync(data, read, write, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }
}
