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

    // FIXME setup tests for new endpoints
    private static void SetupAdmin(SutProvider<AccessPoliciesController> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
    }

    private static void SetupUserWithPermission(SutProvider<AccessPoliciesController> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
    }

    private static void SetupUserWithoutPermission(SutProvider<AccessPoliciesController> sutProvider,
        Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetProjectAccessPolicies_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
                    .ReturnsForAnyArgs(true);
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
    public async void GetProjectAccessPolicies_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedProjectIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetProjectAccessPolicies_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
                    .ReturnsForAnyArgs(true);
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
    public async void GetProjectAccessPolicies_ProjectsExist_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedProjectIdAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedProjectIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountAccessPolicies_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }

        var result = await sutProvider.Sut.GetServiceAccountAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountAccessPolicies_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IServiceAccountRepository>().UserHasWriteAccessToServiceAccount(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedServiceAccountIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountAccessPolicies_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount data,
        UserServiceAccountAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedServiceAccountIdAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetServiceAccountAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountAccessPolicies_ServiceAccountExists_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount data,
        UserServiceAccountAccessPolicy resultAccessPolicy)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IServiceAccountRepository>().UserHasWriteAccessToServiceAccount(default, default)
            .ReturnsForAnyArgs(false);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedServiceAccountIdAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedServiceAccountIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateProjectAccessPolicies_Success(
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
    public async void CreateServiceAccountAccessPoliciesAsync_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        UserServiceAccountAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateForServiceAccountAsync(default, default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await sutProvider.Sut.CreateServiceAccountAccessPoliciesAsync(id, request);

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().Received(1)
            .CreateForServiceAccountAsync(Arg.Any<Guid>(), Arg.Any<List<BaseAccessPolicy>>(), Arg.Any<Guid>());
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
    public async void GetProjectPeoplePotentialGranteesAsync_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }


        var result = await sutProvider.Sut.GetProjectPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetProjectPeoplePotentialGranteesAsync_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeoplePotentialGranteesAsync(id));

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(Arg.Any<Guid>());

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyDetailsByOrganizationAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetProjectPeoplePotentialGranteesAsync_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        Group mockGroup)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }

        sutProvider.GetDependency<IGroupRepository>().GetManyByOrganizationIdAsync(default)
            .ReturnsForAnyArgs(new List<Group> { mockGroup });

        var result = await sutProvider.Sut.GetProjectPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetProjectServiceAccountPotentialGranteesAsync_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }


        var result = await sutProvider.Sut.GetProjectServiceAccountPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetPotentialGranteesAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetProjectServiceAccountPotentialGranteesAsync_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount mockServiceAccount,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }

        sutProvider.GetDependency<IServiceAccountRepository>().GetPotentialGranteesAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ServiceAccount> { mockServiceAccount });

        var result = await sutProvider.Sut.GetProjectServiceAccountPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetPotentialGranteesAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetProjectServiceAccountPotentialGranteesAsync_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountPotentialGranteesAsync(id));

        await sutProvider.GetDependency<IServiceAccountRepository>().DidNotReceiveWithAnyArgs()
            .GetPotentialGranteesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }


    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountPotentialGrantees_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }


        var result = await sutProvider.Sut.GetServiceAccountPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountPotentialGrantees_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IServiceAccountRepository>().UserHasWriteAccessToServiceAccount(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountPotentialGranteesAsync(id));

        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(Arg.Any<Guid>());

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyDetailsByOrganizationAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountPotentialGrantees_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount data,
        Group mockGroup)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .UserHasWriteAccessToServiceAccount(default, default)
                    .ReturnsForAnyArgs(true);
                break;
        }

        sutProvider.GetDependency<IGroupRepository>().GetManyByOrganizationIdAsync(default)
            .ReturnsForAnyArgs(new List<Group> { mockGroup });

        var result = await sutProvider.Sut.GetServiceAccountPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.NotEmpty(result.Data);
    }
}
