using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
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
    private static List<BaseAccessPolicy> MakeDuplicate(List<BaseAccessPolicy> data, AccessPolicyType accessPolicyType)
    {
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

        return data;
    }

    private static void SetupAdmin(SutProvider<CreateAccessPoliciesCommand> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
    }

    private static void SetupUser(SutProvider<CreateAccessPoliciesCommand> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
    }

    private static void SetupPermission(SutProvider<CreateAccessPoliciesCommand> sutProvider,
        PermissionType permissionType, Project project, Guid userId)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, project.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUser(sutProvider, project.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId)
                    .Returns(true);
                break;
        }
    }

    private static void SetupPermission(SutProvider<CreateAccessPoliciesCommand> sutProvider,
        PermissionType permissionType, ServiceAccount serviceAccount, Guid userId)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, serviceAccount.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUser(sutProvider, serviceAccount.OrganizationId);
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(serviceAccount.Id, userId).Returns(true);
                break;
        }
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForProject_SmNotEnabled_Throws(
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

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForProject_AlreadyExists_Throws_BadRequestException(
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

        SetupAdmin(sutProvider, project.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
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
        data = MakeDuplicate(data, accessPolicyType);

        SetupAdmin(sutProvider, project.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateForProject_Success(
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

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        SetupPermission(sutProvider, permissionType, project, userId);

        await sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForProject_UserNoPermission_ThrowsNotFound(
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

        SetupUser(sutProvider, project.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId).Returns(false);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForServiceAccount_SmNotEnabled_Throws(
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
    public async Task CreateForServiceAccount_AlreadyExists_ThrowsBadRequestException(
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

        SetupAdmin(sutProvider, serviceAccount.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
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
    public async Task CreateForServiceAccount_NotUnique_Throws(
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
        data = MakeDuplicate(data, accessPolicyType);

        SetupAdmin(sutProvider, serviceAccount.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
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
    public async Task CreateForServiceAccount_Success(
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

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        SetupPermission(sutProvider, permissionType, serviceAccount, userId);

        await sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateForServiceAccount_UserWithoutPermission_ThrowsNotFound(
        Guid userId,
        ServiceAccount serviceAccount,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userServiceAccountAccessPolicies);
        data.AddRange(groupServiceAccountAccessPolicies);

        SetupUser(sutProvider, serviceAccount.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(serviceAccount.Id).Returns(serviceAccount);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .UserHasWriteAccessToServiceAccount(serviceAccount.Id, userId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForServiceAccountAsync(serviceAccount.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }
}
