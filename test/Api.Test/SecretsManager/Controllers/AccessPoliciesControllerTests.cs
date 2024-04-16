#nullable enable
using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Authorization;
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
    private const int _overMax = 16;

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectAccessPolicies_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                        AccessClientType.NoAccessCheck)
                    .Returns((true, true));
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
                    .Returns((true, true));
                break;
        }

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>());

        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectAccessPolicies_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .Returns((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectAccessPolicies_Success(
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
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                        AccessClientType.NoAccessCheck)
                    .Returns((true, true));
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
                    .Returns((true, true));
                break;
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedProjectIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>());

        Assert.Empty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectAccessPolicies_ProjectsExist_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .Returns((false, false));

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedProjectIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateProjectAccessPolicies_RequestMoreThanMax_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project mockProject,
        UserProjectAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(mockProject);
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>().CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        request = AddRequestsOverMax(request);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateProjectAccessPolicies_ProjectDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        AccessPoliciesCreateRequest request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateProjectAccessPolicies_DuplicatePolicy_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project mockProject,
        UserProjectAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        var dup = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = true, Write = true };
        request.UserAccessPolicyRequests = new[] { dup, dup };
        mockProject.Id = id;
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(mockProject);
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>().CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateProjectAccessPolicies_NoAccess_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project mockProject,
        UserProjectAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        mockProject.Id = id;
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(mockProject);
        var policies = request.ToBaseAccessPoliciesForProject(id, mockProject.OrganizationId);
        foreach (var policy in policies)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), policy,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        }

        sutProvider.GetDependency<ICreateAccessPoliciesCommand>().CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateProjectAccessPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project mockProject,
        UserProjectAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        mockProject.Id = id;
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(mockProject);
        var policies = request.ToBaseAccessPoliciesForProject(id, mockProject.OrganizationId);
        foreach (var policy in policies)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), policy,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }

        sutProvider.GetDependency<ICreateAccessPoliciesCommand>().CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request);

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().Received(1)
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAccessPolicies_NoAccess_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        UserProjectAccessPolicy data,
        AccessPolicyUpdateRequest request)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(id).Returns(data);
        sutProvider.GetDependency<IUpdateAccessPolicyCommand>().UpdateAsync(default, default, default)
            .ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateAccessPolicyAsync(id, request));

        await sutProvider.GetDependency<IUpdateAccessPolicyCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Guid>(), Arg.Is(request.Read), Arg.Is(request.Write));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAccessPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        UserProjectAccessPolicy data,
        AccessPolicyUpdateRequest request)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(id).Returns(data);
        sutProvider.GetDependency<IUpdateAccessPolicyCommand>().UpdateAsync(default, default, default)
            .ReturnsForAnyArgs(data);

        await sutProvider.Sut.UpdateAccessPolicyAsync(id, request);

        await sutProvider.GetDependency<IUpdateAccessPolicyCommand>().Received(1)
            .UpdateAsync(Arg.Any<Guid>(), Arg.Is(request.Read), Arg.Is(request.Write));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAccessPolicies_NoAccess_Throws(SutProvider<AccessPoliciesController> sutProvider, Guid id)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), new UserProjectAccessPolicy(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        sutProvider.GetDependency<IDeleteAccessPolicyCommand>().DeleteAsync(default).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.DeleteAccessPolicyAsync(id));

        await sutProvider.GetDependency<IDeleteAccessPolicyCommand>().DidNotReceiveWithAnyArgs()
            .DeleteAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAccessPolicies_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), new UserProjectAccessPolicy(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IDeleteAccessPolicyCommand>().DeleteAsync(default).ReturnsNull();

        await sutProvider.Sut.DeleteAccessPolicyAsync(id);

        await sutProvider.GetDependency<IDeleteAccessPolicyCommand>().Received(1)
            .DeleteAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetPeoplePotentialGrantees_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        SetupPermission(sutProvider, permissionType, id);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeopleGranteesAsync(default, default)
            .ReturnsForAnyArgs(new PeopleGrantees
            {
                UserGrantees = new List<UserGrantee>(),
                GroupGrantees = new List<GroupGrantee>()
            });

        var result = await sutProvider.Sut.GetPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeopleGranteesAsync(id, Arg.Any<Guid>());
        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetPeoplePotentialGrantees_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeopleGranteesAsync(default, default)
            .ReturnsForAnyArgs(new PeopleGrantees
            {
                UserGrantees = new List<UserGrantee>(),
                GroupGrantees = new List<GroupGrantee>()
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetPeoplePotentialGranteesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeopleGranteesAsync(id, Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetPeoplePotentialGrantees_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        GroupGrantee groupGrantee)
    {
        SetupPermission(sutProvider, permissionType, id);
        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeopleGranteesAsync(default, default)
            .ReturnsForAnyArgs(new PeopleGrantees
            {
                UserGrantees = new List<UserGrantee>(),
                GroupGrantees = new List<GroupGrantee> { groupGrantee }
            });

        var result = await sutProvider.Sut.GetPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeopleGranteesAsync(id, Arg.Any<Guid>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetServiceAccountsPotentialGrantees_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        SetupPermission(sutProvider, permissionType, id);
        var result = await sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountsPotentialGranteesAsync_UserWithoutPermission_Throws(
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
    public async Task GetServiceAccountsPotentialGranteesAsync_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount mockServiceAccount)
    {
        SetupPermission(sutProvider, permissionType, id);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetManyByOrganizationIdWriteAccessAsync(default, default, default)
            .ReturnsForAnyArgs(new List<ServiceAccount> { mockServiceAccount });

        var result = await sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPotentialGrantees_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        SetupPermission(sutProvider, permissionType, id);
        var result = await sutProvider.Sut.GetProjectPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPotentialGrantees_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPotentialGranteesAsync(id));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPotentialGrantees_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project mockProject)
    {
        SetupPermission(sutProvider, permissionType, id);
        sutProvider.GetDependency<IProjectRepository>()
            .GetManyByOrganizationIdWriteAccessAsync(default, default, default)
            .ReturnsForAnyArgs(new List<Project> { mockProject });

        var result = await sutProvider.Sut.GetProjectPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPeopleAccessPolicies_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id, Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                        AccessClientType.NoAccessCheck)
                    .Returns((true, true));
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
                    .Returns((true, true));
                break;
        }

        var result = await sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>());

        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.UserAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPeopleAccessPolicies_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .Returns((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPeopleAccessPolicies_ProjectsExist_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .Returns((false, false));

        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeoplePoliciesByGrantedProjectIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPeopleAccessPolicies_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project data,
        UserProjectAccessPolicy resultUserPolicy,
        GroupProjectAccessPolicy resultGroupPolicy)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                        AccessClientType.NoAccessCheck)
                    .Returns((true, true));
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
                    .Returns((true, true));
                break;
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeoplePoliciesByGrantedProjectIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultUserPolicy, resultGroupPolicy });

        var result = await sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>());

        Assert.NotEmpty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectPeopleAccessPolicies_ProjectDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        PeopleAccessPoliciesRequestModel request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutProjectPeopleAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceProjectPeopleAsync(Arg.Any<ProjectPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectPeopleAccessPoliciesAsync_DuplicatePolicy_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Project project,
        PeopleAccessPoliciesRequestModel request)
    {
        var dup = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = true, Write = true };
        request.UserAccessPolicyRequests = new[] { dup, dup };
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(project);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutProjectPeopleAccessPoliciesAsync(project.Id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceProjectPeopleAsync(Arg.Any<ProjectPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectPeopleAccessPoliciesAsync_NoAccess_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Project project,
        PeopleAccessPoliciesRequestModel request)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(project);
        var peoplePolicies = request.ToProjectPeopleAccessPolicies(project.Id, project.OrganizationId);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), peoplePolicies,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutProjectPeopleAccessPoliciesAsync(project.Id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceProjectPeopleAsync(Arg.Any<ProjectPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectPeopleAccessPoliciesAsync_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid userId,
        Project project,
        PeopleAccessPoliciesRequestModel request)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(project);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        var peoplePolicies = request.ToProjectPeopleAccessPolicies(project.Id, project.OrganizationId);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), peoplePolicies,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        sutProvider.GetDependency<IAccessPolicyRepository>().ReplaceProjectPeopleAsync(peoplePolicies, Arg.Any<Guid>())
            .Returns(peoplePolicies.ToBaseAccessPolicies());

        await sutProvider.Sut.PutProjectPeopleAccessPoliciesAsync(project.Id, request);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .ReplaceProjectPeopleAsync(Arg.Any<ProjectPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_ServiceAccountDoesntExist_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
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

        var result = await sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)), Arg.Any<Guid>());

        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IServiceAccountRepository>().UserHasWriteAccessToServiceAccount(default, default)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
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

        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeoplePoliciesByGrantedServiceAccountIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)), Arg.Any<Guid>());

        Assert.Empty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountPeopleAccessPolicies_ServiceAccountDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        PeopleAccessPoliciesRequestModel request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutServiceAccountPeopleAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceServiceAccountPeopleAsync(Arg.Any<ServiceAccountPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountPeopleAccessPolicies_DuplicatePolicy_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        PeopleAccessPoliciesRequestModel request)
    {
        var dup = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = true, Write = true };
        request.UserAccessPolicyRequests = new[] { dup, dup };
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutServiceAccountPeopleAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceServiceAccountPeopleAsync(Arg.Any<ServiceAccountPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountPeopleAccessPolicies_NotCanReadWrite_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        PeopleAccessPoliciesRequestModel request)
    {
        request.UserAccessPolicyRequests.First().Read = false;
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutServiceAccountPeopleAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceServiceAccountPeopleAsync(Arg.Any<ServiceAccountPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountPeopleAccessPolicies_NoAccess_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        PeopleAccessPoliciesRequestModel request)
    {
        request = SetRequestToCanReadWrite(request);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);
        var peoplePolicies = request.ToServiceAccountPeopleAccessPolicies(data.Id, data.OrganizationId);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), peoplePolicies,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutServiceAccountPeopleAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceServiceAccountPeopleAsync(Arg.Any<ServiceAccountPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountPeopleAccessPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        Guid userId,
        PeopleAccessPoliciesRequestModel request)
    {
        request = SetRequestToCanReadWrite(request);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);
        var peoplePolicies = request.ToServiceAccountPeopleAccessPolicies(data.Id, data.OrganizationId);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), peoplePolicies,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        sutProvider.GetDependency<IAccessPolicyRepository>().ReplaceServiceAccountPeopleAsync(peoplePolicies, Arg.Any<Guid>())
            .Returns(peoplePolicies.ToBaseAccessPolicies());

        await sutProvider.Sut.PutServiceAccountPeopleAccessPoliciesAsync(data.Id, request);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .ReplaceServiceAccountPeopleAsync(Arg.Any<ServiceAccountPeopleAccessPolicies>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountGrantedPoliciesAsync_NoAccess_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).Returns(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetServiceAccountGrantedPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetServiceAccountGrantedPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetServiceAccountGrantedPoliciesAsync_HasAccessNoPolicies_ReturnsEmptyList(
        AccessClientType accessClientType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid userId,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).Returns(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), data.OrganizationId).Returns((accessClientType, userId));

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetServiceAccountGrantedPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>())
            .ReturnsNull();

        var result = await sutProvider.Sut.GetServiceAccountGrantedPoliciesAsync(data.Id);

        Assert.Empty(result.GrantedProjectPolicies);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetServiceAccountGrantedPoliciesAsync_HasAccess_Success(
        AccessClientType accessClientType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid userId,
        ServiceAccountGrantedPoliciesPermissionDetails policies,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).Returns(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), data.OrganizationId).Returns((accessClientType, userId));

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetServiceAccountGrantedPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>())
            .Returns(policies);

        var result = await sutProvider.Sut.GetServiceAccountGrantedPoliciesAsync(data.Id);

        Assert.NotEmpty(result.GrantedProjectPolicies);
        Assert.Equal(policies.ProjectGrantedPolicies.Count(), result.GrantedProjectPolicies.Count);
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountGrantedPoliciesAsync_ServiceAccountDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        ServiceAccountGrantedPoliciesRequestModel request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutServiceAccountGrantedPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateServiceAccountGrantedPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountGrantedPoliciesAsync_DuplicatePolicyRequest_ThrowsBadRequestException(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        ServiceAccountGrantedPoliciesRequestModel request)
    {
        var dup = new GrantedAccessPolicyRequest { GrantedId = Guid.NewGuid(), Read = true, Write = true };
        request.ProjectGrantedPolicyRequests = new[] { dup, dup };

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutServiceAccountGrantedPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateServiceAccountGrantedPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountGrantedPoliciesAsync_InvalidPolicyRequest_ThrowsBadRequestException(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        ServiceAccountGrantedPoliciesRequestModel request)
    {
        var policyRequest = new GrantedAccessPolicyRequest { GrantedId = Guid.NewGuid(), Read = false, Write = true };
        request.ProjectGrantedPolicyRequests = new[] { policyRequest };

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutServiceAccountGrantedPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateServiceAccountGrantedPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountGrantedPoliciesAsync_UserHasNoAccess_ThrowsNotFoundException(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        ServiceAccountGrantedPoliciesRequestModel request)
    {
        request = SetupValidRequest(request);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ServiceAccountGrantedPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutServiceAccountGrantedPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateServiceAccountGrantedPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutServiceAccountGrantedPoliciesAsync_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        ServiceAccountGrantedPoliciesRequestModel request)
    {
        request = SetupValidRequest(request);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ServiceAccountGrantedPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());

        await sutProvider.Sut.PutServiceAccountGrantedPoliciesAsync(data.Id, request);

        await sutProvider.GetDependency<IUpdateServiceAccountGrantedPoliciesCommand>().Received(1)
            .UpdateAsync(Arg.Any<ServiceAccountGrantedPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_ProjectDoesntExist_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetProjectServiceAccountsPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_NoAccess_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .ReturnsForAnyArgs((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetProjectServiceAccountsPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_ClientIsServiceAccount_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<ICurrentContext>().ClientType = ClientType.ServiceAccount;
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .ReturnsForAnyArgs((true, true));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetProjectServiceAccountsPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_HasAccessNoPolicies_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .ReturnsForAnyArgs((true, true));


        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetProjectServiceAccountsPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>())
            .ReturnsNullForAnyArgs();

        var result = await sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id);

        Assert.Empty(result.ServiceAccountPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_HasAccess_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        ProjectServiceAccountsPoliciesPermissionDetails policies,
        Project data)
    {
        SetupUserWithoutPermission(sutProvider, data.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .ReturnsForAnyArgs((true, true));

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetProjectServiceAccountsPoliciesPermissionDetailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs(policies);

        var result = await sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id);

        Assert.NotEmpty(result.ServiceAccountPolicies);
        Assert.Equal(policies.ServiceAccountPoliciesDetails.Count(), result.ServiceAccountPolicies.Count);
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_ProjectDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_DuplicatePolicyRequest_ThrowsBadRequestException(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        var dup = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = true, Write = true };
        request.ServiceAccountPolicyRequests = new[] { dup, dup };

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_InvalidPolicyRequest_ThrowsBadRequestException(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        var policyRequest = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = false, Write = true };
        request.ServiceAccountPolicyRequests = new[] { policyRequest };

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_UserHasNoAccess_ThrowsNotFoundException(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        request = SetupValidRequest(request);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProjectServiceAccountsPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsPoliciesUpdates>());
        ;
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        request = SetupValidRequest(request);
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProjectServiceAccountsPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());

        await sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request);

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsPoliciesCommand>().Received(1)
            .UpdateAsync(Arg.Any<ProjectServiceAccountsPoliciesUpdates>());
    }

    private static AccessPoliciesCreateRequest AddRequestsOverMax(AccessPoliciesCreateRequest request)
    {
        var newRequests = new List<AccessPolicyRequest>();
        for (var i = 0; i < _overMax; i++)
        {
            newRequests.Add(new AccessPolicyRequest { GranteeId = new Guid(), Read = true, Write = true });
        }

        request.UserAccessPolicyRequests = newRequests;
        return request;
    }

    private static List<GrantedAccessPolicyRequest> AddRequestsOverMax(List<GrantedAccessPolicyRequest> request)
    {
        for (var i = 0; i < _overMax; i++)
        {
            request.Add(new GrantedAccessPolicyRequest { GrantedId = new Guid() });
        }

        return request;
    }

    private static PeopleAccessPoliciesRequestModel SetRequestToCanReadWrite(PeopleAccessPoliciesRequestModel request)
    {
        foreach (var ap in request.UserAccessPolicyRequests)
        {
            ap.Read = true;
            ap.Write = true;
        }

        foreach (var ap in request.GroupAccessPolicyRequests)
        {
            ap.Read = true;
            ap.Write = true;
        }

        return request;
    }

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

    private static void SetupPermission(SutProvider<AccessPoliciesController> sutProvider,
        PermissionType permissionType, Guid orgId)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, orgId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, orgId);
                break;
        }
    }

    private static ServiceAccountGrantedPoliciesRequestModel SetupValidRequest(ServiceAccountGrantedPoliciesRequestModel request)
    {
        foreach (var policyRequest in request.ProjectGrantedPolicyRequests)
        {
            policyRequest.Read = true;
        }

        return request;
    }

    private static ProjectServiceAccountsAccessPoliciesRequestModel SetupValidRequest(ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        foreach (var policyRequest in request.ServiceAccountPolicyRequests)
        {
            policyRequest.Read = true;
        }

        return request;
    }
}
