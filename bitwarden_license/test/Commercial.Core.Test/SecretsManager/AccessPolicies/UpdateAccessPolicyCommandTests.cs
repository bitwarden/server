using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
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
    [BitAutoData(TestAccessPolicyType.UserProjectAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.UserProjectAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    [BitAutoData(TestAccessPolicyType.GroupProjectAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.GroupProjectAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    [BitAutoData(TestAccessPolicyType.ServiceAccountProjectAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.ServiceAccountProjectAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    public async Task UpdateAsync_ProjectGrants_PermissionsCheck_Success(
        TestAccessPolicyType testAccessPolicyType,
        TestPermissionType testPermissionType,
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
        switch (testAccessPolicyType)
        {
            case TestAccessPolicyType.UserProjectAccessPolicy:
                policyToReturn =
                    new UserProjectAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedProjectId = grantedProject.Id,
                    };
                break;
            case TestAccessPolicyType.GroupProjectAccessPolicy:
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
            case TestAccessPolicyType.ServiceAccountProjectAccessPolicy:
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
        switch (testPermissionType)
        {
            case TestPermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(grantedProject.OrganizationId)
                    .Returns(true);
                break;
            case TestPermissionType.RunAsUserWithPermission:
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
    [BitAutoData(TestAccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(TestAccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(TestAccessPolicyType.ServiceAccountProjectAccessPolicy)]
    public async Task UpdateAsync_ProjectGrants_PermissionsCheck_ThrowsNotAuthorized(
        TestAccessPolicyType testAccessPolicyType,
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
        switch (testAccessPolicyType)
        {
            case TestAccessPolicyType.UserProjectAccessPolicy:
                policyToReturn =
                    new UserProjectAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedProjectId = grantedProject.Id,
                    };
                break;
            case TestAccessPolicyType.GroupProjectAccessPolicy:
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
            case TestAccessPolicyType.ServiceAccountProjectAccessPolicy:
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

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.UpdateAsync(data, read, write, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData(TestAccessPolicyType.UserServiceAccountAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.UserServiceAccountAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    [BitAutoData(TestAccessPolicyType.GroupServiceAccountAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.GroupServiceAccountAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    public async Task UpdateAsync_ServiceAccountGrants_PermissionsCheck_Success(
        TestAccessPolicyType testAccessPolicyType,
        TestPermissionType testPermissionType,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        switch (testAccessPolicyType)
        {
            case TestAccessPolicyType.UserServiceAccountAccessPolicy:
                policyToReturn =
                    new UserServiceAccountAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                    };
                break;
            case TestAccessPolicyType.GroupServiceAccountAccessPolicy:
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
        switch (testPermissionType)
        {
            case TestPermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(grantedServiceAccount.OrganizationId)
                    .Returns(true);
                break;
            case TestPermissionType.RunAsUserWithPermission:
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
    [BitAutoData(TestAccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(TestAccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task UpdateAsync_ServiceAccountGrants_PermissionsCheck_ThrowsNotAuthorized(
        TestAccessPolicyType testAccessPolicyType,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        switch (testAccessPolicyType)
        {
            case TestAccessPolicyType.UserServiceAccountAccessPolicy:
                policyToReturn =
                    new UserServiceAccountAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                    };
                break;
            case TestAccessPolicyType.GroupServiceAccountAccessPolicy:
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

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.UpdateAsync(data, read, write, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }
}
