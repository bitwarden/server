﻿using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
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

namespace Bit.Commercial.Core.Test.SecretsManager.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class CreateAccessPoliciesCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateForProjectAsync_SmNotEnabled_Throws(
        Guid userId,
        Project project,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForServiceAccountAsync_SmNotEnabled_Throws(
        Guid userId,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForProjectAsync_AlreadyExists_Throws_BadRequestException(
        Guid userId,
        Project project,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForServiceAccountAsync_AlreadyExists_Throws_BadRequestException(
        Guid userId,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(serviceAccount.OrganizationId).Returns(true);

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CreateForProjectAsync_NotUnique_ThrowsException(
        AccessPolicyType accessPolicyType,
        Guid userId,
        Project project,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider
    )
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);

        switch (accessPolicyType)
        {
            case AccessPolicyType.UserProjectAccessPolicy:
                {
                    var mockAccessPolicy = new UserProjectAccessPolicy
                    {
                        OrganizationUserId = Guid.NewGuid(),
                        GrantedProjectId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.GroupProjectAccessPolicy:
                {
                    var mockAccessPolicy = new GroupProjectAccessPolicy
                    {
                        GroupId = Guid.NewGuid(),
                        GrantedProjectId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.ServiceAccountProjectAccessPolicy:
                {
                    var mockAccessPolicy = new ServiceAccountProjectAccessPolicy
                    {
                        ServiceAccountId = Guid.NewGuid(),
                        GrantedProjectId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.UserServiceAccountAccessPolicy:
                {
                    var mockAccessPolicy = new UserServiceAccountAccessPolicy
                    {
                        OrganizationUserId = Guid.NewGuid(),
                        GrantedServiceAccountId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.GroupServiceAccountAccessPolicy:
                {
                    var mockAccessPolicy = new GroupServiceAccountAccessPolicy
                    {
                        GroupId = Guid.NewGuid(),
                        GrantedServiceAccountId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CreateForServiceAccountAsync_NotUnique_ThrowsException(
        AccessPolicyType accessPolicyType,
        Guid userId,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider
    )
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(serviceAccount.OrganizationId).Returns(true);

        switch (accessPolicyType)
        {
            case AccessPolicyType.UserProjectAccessPolicy:
                {
                    var mockAccessPolicy = new UserProjectAccessPolicy
                    {
                        OrganizationUserId = Guid.NewGuid(),
                        GrantedProjectId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.GroupProjectAccessPolicy:
                {
                    var mockAccessPolicy = new GroupProjectAccessPolicy
                    {
                        GroupId = Guid.NewGuid(),
                        GrantedProjectId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.ServiceAccountProjectAccessPolicy:
                {
                    var mockAccessPolicy = new ServiceAccountProjectAccessPolicy
                    {
                        ServiceAccountId = Guid.NewGuid(),
                        GrantedProjectId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.UserServiceAccountAccessPolicy:
                {
                    var mockAccessPolicy = new UserServiceAccountAccessPolicy
                    {
                        OrganizationUserId = Guid.NewGuid(),
                        GrantedServiceAccountId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
            case AccessPolicyType.GroupServiceAccountAccessPolicy:
                {
                    var mockAccessPolicy = new GroupServiceAccountAccessPolicy
                    {
                        GroupId = Guid.NewGuid(),
                        GrantedServiceAccountId = Guid.NewGuid(),
                    };
                    data.Add(mockAccessPolicy);

                    // Add a duplicate policy
                    data.Add(mockAccessPolicy);
                    break;
                }
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateForProjectAsync_Success(
        PermissionType permissionType,
        Guid userId,
        Project project,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId)
                    .Returns(true);
                break;
        }

        await sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateForServiceAccountAsync_Success(
        PermissionType permissionType,
        Guid userId,
        ServiceAccount serviceAccount,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userServiceAccountAccessPolicies);
        data.AddRange(groupServiceAccountAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(serviceAccount.OrganizationId)
                    .Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(serviceAccount.Id, userId).Returns(true);
                break;
        }

        await sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForProjectAsync_User_NoPermission(
        Guid userId,
        Project project,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForServiceAccountAsync_User_NoPermission(
        Guid userId,
        ServiceAccount serviceAccount,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userServiceAccountAccessPolicies);
        data.AddRange(groupServiceAccountAccessPolicies);

        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).Returns(true);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .UserHasWriteAccessToServiceAccount(serviceAccount.Id, userId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }
}
