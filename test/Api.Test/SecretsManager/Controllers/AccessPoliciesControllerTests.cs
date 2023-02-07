using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
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
    public enum PermissionType
    {
        RunAsAdmin,
        RunAsUserWithPermission,
    }

    private static void SetupAdmin(SutProvider<AccessPoliciesController> sutProvider, Project data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(true);
    }

    private static void SetupUserWithPermission(SutProvider<AccessPoliciesController> sutProvider, Project data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(true);
    }

    private static void SetupUserWithoutPermission(SutProvider<AccessPoliciesController> sutProvider, Project data)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetAccessPoliciesByProject_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data);
                break;
        }

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

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
        SetupUserWithoutPermission(sutProvider, data);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedProjectIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetAccessPoliciesByProject_Admin_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data);
                break;
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedProjectIdAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

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
        SetupUserWithoutPermission(sutProvider, data);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedProjectIdAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedProjectIdAsync(Arg.Any<Guid>());
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

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetPeoplePotentialGranteesAsync_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(true);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
        }

        var result = await sutProvider.Sut.GetPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetPeoplePotentialGranteesAsync_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetPeoplePotentialGranteesAsync(id));

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(Arg.Any<Guid>());

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyDetailsByOrganizationAsync(Arg.Any<Guid>());

        await sutProvider.GetDependency<IServiceAccountRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetPeoplePotentialGranteesAsync_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Group mockGroup)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(true);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
        }

        sutProvider.GetDependency<IGroupRepository>().GetManyByOrganizationIdAsync(default)
            .ReturnsForAnyArgs(new List<Group> { mockGroup });

        var result = await sutProvider.Sut.GetPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountsPotentialGranteesAsync_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(true);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
        }

        var result = await sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsPotentialGranteesAsync_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id));

        await sutProvider.GetDependency<IServiceAccountRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountsPotentialGranteesAsync_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount mockServiceAccount)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(true);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
                sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
                sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
                break;
        }

        sutProvider.GetDependency<IServiceAccountRepository>().GetManyByOrganizationIdWriteAccessAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ServiceAccount> { mockServiceAccount });

        var result = await sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }
}
