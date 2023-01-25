using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Context;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(AccessPoliciesController))]
[SutProviderCustomize]
[ProjectCustomize]
[JsonDocumentCustomize]
public class AccessPoliciesControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_Admin_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(true);

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByProjectId(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_UserWithPermission_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(true);

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByProjectId(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByProjectId(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_Admin_Success(SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(true);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByProjectId(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByProjectId(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_UserWithPermission_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByProjectId(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByProjectId(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_ProjectsExist_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByProjectId(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByProjectId(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateAccessPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        UserProjectAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>().CreateForProjectAsync(default, default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request);

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().Received(1)
            .CreateForProjectAsync(Arg.Any<Guid>(), Arg.Any<List<BaseAccessPolicy>>(), Arg.Any<Guid>());
    }


    [Theory]
    [BitAutoData]
    public async void UpdateAccessPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        UserProjectAccessPolicy data,
        AccessPolicyUpdateRequest request)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IUpdateAccessPolicyCommand>().UpdateAsync(default, default, default, default)
            .ReturnsForAnyArgs(data);

        await sutProvider.Sut.UpdateAccessPolicyAsync(id, request);

        await sutProvider.GetDependency<IUpdateAccessPolicyCommand>().Received(1)
            .UpdateAsync(Arg.Any<Guid>(), Arg.Is(request.Read), Arg.Is(request.Write), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void DeleteAccessPolicies_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IDeleteAccessPolicyCommand>().DeleteAsync(default, default).ReturnsNull();

        await sutProvider.Sut.DeleteAccessPolicyAsync(id);

        await sutProvider.GetDependency<IDeleteAccessPolicyCommand>().Received(1)
            .DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }
}
