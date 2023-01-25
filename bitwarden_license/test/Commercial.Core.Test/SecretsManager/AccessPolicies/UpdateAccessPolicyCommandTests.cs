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
    [BitAutoData(true, false, false, true, false)]
    [BitAutoData(false, true, false, true, false)]
    [BitAutoData(false, false, true, true, false)]
    [BitAutoData(true, false, false, false, true)]
    [BitAutoData(false, true, false, false, true)]
    [BitAutoData(false, false, true, false, true)]
    public async Task UpdateAsync_PermissionsCheck_Success(
        bool userProjectAccessPolicy,
        bool groupProjectAccessPolicy,
        bool serviceAccountProjectAccessPolicy,
        bool runAsAdmin,
        bool runAsUserWithPermission,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        Project project,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        if (userProjectAccessPolicy)
        {
            policyToReturn =
                new UserProjectAccessPolicy { Id = data, Read = true, Write = true, GrantedProjectId = project.Id };
        }
        else if (groupProjectAccessPolicy)
        {
            mockGroup.OrganizationId = project.OrganizationId;
            policyToReturn =
                new GroupProjectAccessPolicy
                {
                    Id = data,
                    GrantedProjectId = project.Id,
                    Read = true,
                    Write = true,
                    Group = mockGroup,
                };
        }
        else if (serviceAccountProjectAccessPolicy)
        {
            mockServiceAccount.OrganizationId = project.OrganizationId;
            policyToReturn = new ServiceAccountProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = project.Id,
                Read = true,
                Write = true,
                ServiceAccount = mockServiceAccount,
            };
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        if (runAsAdmin)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);
        }
        else if (runAsUserWithPermission)
        {
            sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId)
                .Returns(true);
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);
        var result = await sutProvider.Sut.UpdateAsync(data, read, write, userId);
        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).ReplaceAsync(policyToReturn);

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }

    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]
    public async Task UpdateAsync_PermissionsCheck_ThrowsNotAuthorized(
        bool userProjectAccessPolicy,
        bool groupProjectAccessPolicy,
        bool serviceAccountProjectAccessPolicy,
        Guid data,
        bool read,
        bool write,
        Guid userId,
        Project project,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        BaseAccessPolicy policyToReturn = null;
        if (userProjectAccessPolicy)
        {
            policyToReturn =
                new UserProjectAccessPolicy { Id = data, Read = true, Write = true, GrantedProjectId = project.Id };
        }
        else if (groupProjectAccessPolicy)
        {
            mockGroup.OrganizationId = project.OrganizationId;
            policyToReturn =
                new GroupProjectAccessPolicy
                {
                    Id = data,
                    GrantedProjectId = project.Id,
                    Read = true,
                    Write = true,
                    Group = mockGroup,
                };
        }
        else if (serviceAccountProjectAccessPolicy)
        {
            mockServiceAccount.OrganizationId = project.OrganizationId;
            policyToReturn = new ServiceAccountProjectAccessPolicy
            {
                Id = data,
                GrantedProjectId = project.Id,
                Read = true,
                Write = true,
                ServiceAccount = mockServiceAccount,
            };
        }

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.UpdateAsync(data, read, write, userId));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }
}
