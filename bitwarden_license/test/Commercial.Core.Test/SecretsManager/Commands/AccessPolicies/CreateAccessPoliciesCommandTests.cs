using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class CreateAccessPoliciesCommandTests
{
    private static List<BaseAccessPolicy> MakeGrantedProjectAccessPolicies(Guid grantedProjectId, List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies)
    {
        var data = new List<BaseAccessPolicy>();
        foreach (var ap in userProjectAccessPolicies)
        {
            ap.GrantedProjectId = grantedProjectId;
        }
        foreach (var ap in groupProjectAccessPolicies)
        {
            ap.GrantedProjectId = grantedProjectId;
        }
        foreach (var ap in serviceAccountProjectAccessPolicies)
        {
            ap.GrantedProjectId = grantedProjectId;
        }
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);
        return data;
    }

    private static List<BaseAccessPolicy> MakeGrantedServiceAccountAccessPolicies(Guid grantedServiceAccountId, List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies)
    {
        var data = new List<BaseAccessPolicy>();
        foreach (var ap in userServiceAccountAccessPolicies)
        {
            ap.GrantedServiceAccountId = grantedServiceAccountId;
        }
        foreach (var ap in groupServiceAccountAccessPolicies)
        {
            ap.GrantedServiceAccountId = grantedServiceAccountId;
        }
        data.AddRange(userServiceAccountAccessPolicies);
        data.AddRange(groupServiceAccountAccessPolicies);
        return data;
    }

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

    private static void SetupPermission(SutProvider<CreateAccessPoliciesCommand> sutProvider,
        PermissionType permissionType, Project project, Guid userId)
    {
        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(project.Id, userId, AccessClientType.User)
                .Returns((true, true));
        }
    }

    private static void SetupPermission(SutProvider<CreateAccessPoliciesCommand> sutProvider,
        PermissionType permissionType, ServiceAccount serviceAccount, Guid userId)
    {
        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            sutProvider.GetDependency<IServiceAccountRepository>()
                .UserHasWriteAccessToServiceAccount(serviceAccount.Id, userId).Returns(true);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task CreateMany_AlreadyExists_Throws_BadRequestException(
        Guid userId,
        Project project,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = MakeGrantedProjectAccessPolicies(project.Id, userProjectAccessPolicies, groupProjectAccessPolicies,
            serviceAccountProjectAccessPolicies);
        var saData = MakeGrantedServiceAccountAccessPolicies(serviceAccount.Id, userServiceAccountAccessPolicies, groupServiceAccountAccessPolicies);
        data = data.Concat(saData).ToList();

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateManyAsync(data, userId, AccessClientType.NoAccessCheck));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default!);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CreateMany_NotUnique_ThrowsException(
        AccessPolicyType accessPolicyType,
        Guid userId,
        Project project,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider
    )
    {
        var data = MakeGrantedProjectAccessPolicies(project.Id, userProjectAccessPolicies, groupProjectAccessPolicies,
            serviceAccountProjectAccessPolicies);
        var saData = MakeGrantedServiceAccountAccessPolicies(serviceAccount.Id, userServiceAccountAccessPolicies, groupServiceAccountAccessPolicies);
        data = data.Concat(saData).ToList();
        data = MakeDuplicate(data, accessPolicyType);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateManyAsync(data, userId, AccessClientType.NoAccessCheck));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateMany_Success(
        PermissionType permissionType,
        Guid userId,
        Project project,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = MakeGrantedProjectAccessPolicies(project.Id, userProjectAccessPolicies, groupProjectAccessPolicies,
            serviceAccountProjectAccessPolicies);
        var saData = MakeGrantedServiceAccountAccessPolicies(serviceAccount.Id, userServiceAccountAccessPolicies, groupServiceAccountAccessPolicies);
        data = data.Concat(saData).ToList();

        SetupPermission(sutProvider, permissionType, serviceAccount, userId);
        SetupPermission(sutProvider, permissionType, project, userId);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            await sutProvider.Sut.CreateManyAsync(data, userId, AccessClientType.NoAccessCheck);
        }
        else if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            await sutProvider.Sut.CreateManyAsync(data, userId, AccessClientType.User);
        }

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateMany_UserWithoutPermission_Throws(
        Guid userId,
        Project project,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = MakeGrantedProjectAccessPolicies(project.Id, userProjectAccessPolicies, groupProjectAccessPolicies,
            serviceAccountProjectAccessPolicies);
        var saData = MakeGrantedServiceAccountAccessPolicies(serviceAccount.Id, userServiceAccountAccessPolicies, groupServiceAccountAccessPolicies);
        data = data.Concat(saData).ToList();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateManyAsync(data, userId, AccessClientType.User));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }
}
