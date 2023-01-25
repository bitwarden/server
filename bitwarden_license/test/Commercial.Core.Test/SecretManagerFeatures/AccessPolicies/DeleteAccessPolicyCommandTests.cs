using Bit.Commercial.Core.SecretManagerFeatures.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.AccessPolicies;

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
    [BitAutoData(true, false, false, true, false)]
    [BitAutoData(false, true, false, true, false)]
    [BitAutoData(false, false, true, true, false)]
    [BitAutoData(true, false, false, false, true)]
    [BitAutoData(false, true, false, false, true)]
    [BitAutoData(false, false, true, false, true)]
    public async Task DeleteAccessPolicy_PermissionsCheck_Success(
        bool userProjectAccessPolicy,
        bool groupProjectAccessPolicy,
        bool serviceAccountProjectAccessPolicy,
        bool runAsAdmin,
        bool runAsUserWithPermission,
        Guid data,
        Guid userId,
        Project project,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        if (userProjectAccessPolicy)
        {
            policyToReturn = new UserProjectAccessPolicy { Id = data, GrantedProjectId = project.Id };
        }
        else if (groupProjectAccessPolicy)
        {
            mockGroup.OrganizationId = project.OrganizationId;
            policyToReturn =
                new GroupProjectAccessPolicy { Id = data, GrantedProjectId = project.Id, Group = mockGroup };
        }
        else if (serviceAccountProjectAccessPolicy)
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
        if (runAsAdmin)
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);
        else if (runAsUserWithPermission)
            sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId)
                .Returns(true);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await sutProvider.Sut.DeleteAsync(data, userId);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).DeleteAsync(Arg.Is(data));
    }

    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    public async Task DeleteAccessPolicy_UserProjectAccessPolicy_PermissionsCheck_ThrowsNotAuthorized(
        bool userProjectAccessPolicy,
        bool groupProjectAccessPolicy,
        bool serviceAccountProjectAccessPolicy,
        Guid data,
        Guid userId,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        Project project,
        SutProvider<DeleteAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;

        if (userProjectAccessPolicy)
            policyToReturn = new UserProjectAccessPolicy { Id = data, GrantedProjectId = project.Id };
        else if (groupProjectAccessPolicy)
            policyToReturn =
                new GroupProjectAccessPolicy { Id = data, GrantedProjectId = project.Id, Group = mockGroup };
        else if (serviceAccountProjectAccessPolicy)
            policyToReturn = new ServiceAccountProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = project.Id,
                ServiceAccount = mockServiceAccount,
            };

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data)
            .Returns(policyToReturn);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.DeleteAsync(data, userId));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }
}
