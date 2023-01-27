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
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_AlreadyExists_Throws_BadRequestException(
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
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }

    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(true, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(true, false, true)]
    [BitAutoData(false, true, true)]
    [BitAutoData(true, true, true)]
    public async Task CreateAsync_NotUnique_ThrowsException(
        bool testUserPolicies,
        bool testGroupPolicies,
        bool testServiceAccountPolicies,
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

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);

        if (testUserPolicies)
        {
            var mockUserPolicy = new UserProjectAccessPolicy
            {
                OrganizationUserId = Guid.NewGuid(),
                GrantedProjectId = Guid.NewGuid(),
            };
            data.Add(mockUserPolicy);

            // Add a duplicate policy
            data.Add(mockUserPolicy);
        }

        if (testGroupPolicies)
        {
            var mockGroupPolicy = new GroupProjectAccessPolicy
            {
                GroupId = Guid.NewGuid(),
                GrantedProjectId = Guid.NewGuid(),
            };
            data.Add(mockGroupPolicy);

            // Add a duplicate policy
            data.Add(mockGroupPolicy);
        }

        if (testServiceAccountPolicies)
        {
            var mockServiceAccountPolicy = new ServiceAccountProjectAccessPolicy
            {
                ServiceAccountId = Guid.NewGuid(),
                GrantedProjectId = Guid.NewGuid(),
            };
            data.Add(mockServiceAccountPolicy);

            // Add a duplicate policy
            data.Add(mockServiceAccountPolicy);
        }


        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateAsync_Success(
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

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId).Returns(true);
                break;
        }

        await sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_User_NoPermission(
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
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateForProjectAsync(project.Id, data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }
}
