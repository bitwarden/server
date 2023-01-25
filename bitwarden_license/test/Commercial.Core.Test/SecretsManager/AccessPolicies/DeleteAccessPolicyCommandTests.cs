using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
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
    [Theory]
    [BitAutoData]
    public async Task DeleteAccessPolicy_Throws_NotFoundException(Guid data, Guid userId,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAsync(data, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory]
    [BitAutoData(TestAccessPolicyType.UserProjectAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.UserProjectAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    [BitAutoData(TestAccessPolicyType.GroupProjectAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.GroupProjectAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    [BitAutoData(TestAccessPolicyType.ServiceAccountProjectAccessPolicy, TestPermissionType.RunAsAdmin)]
    [BitAutoData(TestAccessPolicyType.ServiceAccountProjectAccessPolicy, TestPermissionType.RunAsUserWithPermission)]
    public async Task DeleteAccessPolicy_ProjectGrants_PermissionsCheck_Success(
        TestAccessPolicyType testAccessPolicyType,
        TestPermissionType testPermissionType,
        Guid data,
        Guid userId,
        Project project,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        if (testAccessPolicyType == TestAccessPolicyType.UserProjectAccessPolicy)
        {
            policyToReturn = new UserProjectAccessPolicy { Id = data, GrantedProjectId = project.Id };
        }
        else if (testAccessPolicyType == TestAccessPolicyType.GroupProjectAccessPolicy)
        {
            mockGroup.OrganizationId = project.OrganizationId;
            policyToReturn =
                new GroupProjectAccessPolicy { Id = data, GrantedProjectId = project.Id, Group = mockGroup };
        }
        else if (testAccessPolicyType == TestAccessPolicyType.ServiceAccountProjectAccessPolicy)
        {
            mockServiceAccount.OrganizationId = project.OrganizationId;
            policyToReturn = new ServiceAccountProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = project.Id,
                ServiceAccount = mockServiceAccount,
            };
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        switch (testPermissionType)
        {
            case TestPermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);
                break;
            case TestPermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId)
                    .Returns(true);
                break;
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await sutProvider.Sut.DeleteAsync(data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).DeleteAsync(Arg.Is(data));
    }

    [Theory]
    [BitAutoData(TestAccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(TestAccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(TestAccessPolicyType.ServiceAccountProjectAccessPolicy)]
    public async Task DeleteAccessPolicy_UserProjectAccessPolicy_PermissionsCheck_ThrowsNotAuthorized(
        TestAccessPolicyType testAccessPolicyType,
        Guid data,
        Guid userId,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        Project project,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;

        switch (testAccessPolicyType)
        {
            case TestAccessPolicyType.UserProjectAccessPolicy:
                policyToReturn = new UserProjectAccessPolicy { Id = data, GrantedProjectId = project.Id };
                break;
            case TestAccessPolicyType.GroupProjectAccessPolicy:
                policyToReturn =
                    new GroupProjectAccessPolicy { Id = data, GrantedProjectId = project.Id, Group = mockGroup };
                break;
            case TestAccessPolicyType.ServiceAccountProjectAccessPolicy:
                policyToReturn = new ServiceAccountProjectAccessPolicy
                {
                    Id = data,
                    GrantedProjectId = project.Id,
                    ServiceAccount = mockServiceAccount,
                };
                break;
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.DeleteAsync(data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }
}
