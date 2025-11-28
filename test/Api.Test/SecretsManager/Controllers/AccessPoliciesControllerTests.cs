#nullable enable
using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
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
    [Theory]
    [BitAutoData]
    public async Task GetPeoplePotentialGrantees_UserWithoutPermission_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetPeoplePotentialGranteesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeopleGranteesAsync(id, Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetPeoplePotentialGrantees_HasAccessNoPotentialGrantees_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(Guid.NewGuid());
        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeopleGranteesAsync(id, Arg.Any<Guid>())
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
    public async Task GetPeoplePotentialGrantees_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        GroupGrantee groupGrantee)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(Guid.NewGuid());
        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeopleGranteesAsync(id, Arg.Any<Guid>())
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
    [BitAutoData]
    public async Task GetServiceAccountsPotentialGranteesAsync_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id));

        await sutProvider.GetDependency<IServiceAccountRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountsPotentialGrantees_HasAccessNoPotentialGrantees_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);

        var result = await sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountsPotentialGranteesAsync_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs(new List<ServiceAccount> { serviceAccount });

        var result = await sutProvider.Sut.GetServiceAccountsPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPotentialGrantees_UserWithoutPermission_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPotentialGranteesAsync(id));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPotentialGrantees_HasAccessNoPotentialGrantees_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);

        var result = await sutProvider.Sut.GetProjectPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPotentialGrantees_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project project)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(id).Returns(true);
        sutProvider.GetDependency<IProjectRepository>()
            .GetManyByOrganizationIdWriteAccessAsync(default, default, default)
            .ReturnsForAnyArgs(new List<Project> { project });

        var result = await sutProvider.Sut.GetProjectPotentialGranteesAsync(id);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .GetManyByOrganizationIdWriteAccessAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Is(AssertHelper.AssertPropertyEqual(id)),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPeopleAccessPolicies_ProjectDoesNotExist_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPeopleAccessPolicies_NoAccessSecretsManager_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>())
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task GetProjectPeopleAccessPolicies_UserWithoutPermission_ThrowsNotFound(
        bool canRead,
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>())
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs((canRead, false));

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectPeopleAccessPolicies_ServiceAccountClient_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupProjectAccessPoliciesTest(sutProvider, data, AccessClientType.ServiceAccount);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetProjectPeopleAccessPolicies_ReturnsEmptyList(
        AccessClientType accessClientType,
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupProjectAccessPoliciesTest(sutProvider, data, accessClientType);

        var result = await sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(data.Id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)),
                Arg.Any<Guid>());

        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.UserAccessPolicies);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetProjectPeopleAccessPolicies_Success(
        AccessClientType accessClientType,
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        UserProjectAccessPolicy resultUserPolicy,
        GroupProjectAccessPolicy resultGroupPolicy)
    {
        SetupProjectAccessPoliciesTest(sutProvider, data, accessClientType);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetPeoplePoliciesByGrantedProjectIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultUserPolicy, resultGroupPolicy });

        var result = await sutProvider.Sut.GetProjectPeopleAccessPoliciesAsync(data.Id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedProjectIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)),
                Arg.Any<Guid>());

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
    [BitAutoData]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_NoAccessSecretsManager_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_UserWithoutPermission_Throws(
        bool canRead,
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(data.OrganizationId)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs((canRead, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_HasPermissionNoPolicies_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs((true, true));

        var result = await sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)),
                Arg.Any<Guid>());

        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountPeopleAccessPoliciesAsync_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        ServiceAccount data,
        UserServiceAccountAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs((true, true));

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetServiceAccountPeopleAccessPoliciesAsync(data.Id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetPeoplePoliciesByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Id)),
                Arg.Any<Guid>());

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

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .ReplaceServiceAccountPeopleAsync(peoplePolicies, Arg.Any<Guid>())
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
            .GetProjectServiceAccountsAccessPoliciesAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_NoAccessSecretsManager_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>()).ReturnsForAnyArgs(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetProjectServiceAccountsAccessPoliciesAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_NoAccess_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(default, default, default)
            .ReturnsForAnyArgs((false, false));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetProjectServiceAccountsAccessPoliciesAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_ClientIsServiceAccount_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupProjectAccessPoliciesTest(sutProvider, data, AccessClientType.ServiceAccount);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetProjectServiceAccountsAccessPoliciesAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_HasAccessNoPolicies_ReturnsEmptyList(
        AccessClientType accessClientType,
        SutProvider<AccessPoliciesController> sutProvider,
        Project data)
    {
        SetupProjectAccessPoliciesTest(sutProvider, data, accessClientType);

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetProjectServiceAccountsAccessPoliciesAsync(Arg.Any<Guid>())
            .ReturnsNullForAnyArgs();

        var result = await sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id);

        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task GetProjectServiceAccountsAccessPoliciesAsync_HasAccess_Success(
        AccessClientType accessClientType,
        SutProvider<AccessPoliciesController> sutProvider,
        ProjectServiceAccountsAccessPolicies policies,
        Project data)
    {
        SetupProjectAccessPoliciesTest(sutProvider, data, accessClientType);

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetProjectServiceAccountsAccessPoliciesAsync(Arg.Any<Guid>())
            .ReturnsForAnyArgs(policies);

        var result = await sutProvider.Sut.GetProjectServiceAccountsAccessPoliciesAsync(data.Id);

        Assert.NotEmpty(result.ServiceAccountAccessPolicies);
        Assert.Equal(policies.ServiceAccountAccessPolicies.Count(), result.ServiceAccountAccessPolicies.Count);
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

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_DuplicatePolicyRequest_ThrowsBadRequestException(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        var dup = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = true, Write = true };
        request.ServiceAccountAccessPolicyRequests = [dup, dup];

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutProjectServiceAccountsAccessPoliciesAsync_InvalidPolicyRequest_ThrowsBadRequestException(
        SutProvider<AccessPoliciesController> sutProvider,
        Project data,
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        var policyRequest = new AccessPolicyRequest { GranteeId = Guid.NewGuid(), Read = false, Write = true };
        request.ServiceAccountAccessPolicyRequests = [policyRequest];

        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(data.Id).ReturnsForAnyArgs(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>());
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
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request));

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>());
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
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(AuthorizationResult.Success());

        await sutProvider.Sut.PutProjectServiceAccountsAccessPoliciesAsync(data.Id, request);

        await sutProvider.GetDependency<IUpdateProjectServiceAccountsAccessPoliciesCommand>().Received(1)
            .UpdateAsync(Arg.Any<ProjectServiceAccountsAccessPoliciesUpdates>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretAccessPoliciesAsync_NoAccess_ThrowsNotFound(
        SutProvider<AccessPoliciesController> sutProvider,
        Secret data)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetSecretAccessPoliciesAsync(data.Id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(0)
            .GetSecretAccessPoliciesAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretAccessPoliciesAsync_HasAccessNoPolicies_ReturnsEmptyList(
        SutProvider<AccessPoliciesController> sutProvider,
        Secret data)
    {
        SetupSecretAccessPoliciesTest(sutProvider, data);
        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetSecretAccessPoliciesAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .ReturnsNull();

        var result = await sutProvider.Sut.GetSecretAccessPoliciesAsync(data.Id);

        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSecretAccessPoliciesAsync_HasAccess_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        SecretAccessPolicies policies,
        Secret data)
    {
        SetupSecretAccessPoliciesTest(sutProvider, data);
        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetSecretAccessPoliciesAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(policies);

        var result = await sutProvider.Sut.GetSecretAccessPoliciesAsync(data.Id);

        Assert.NotEmpty(result.UserAccessPolicies);
        Assert.NotEmpty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.ServiceAccountAccessPolicies);
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

    private static ServiceAccountGrantedPoliciesRequestModel SetupValidRequest(
        ServiceAccountGrantedPoliciesRequestModel request)
    {
        foreach (var policyRequest in request.ProjectGrantedPolicyRequests)
        {
            policyRequest.Read = true;
        }

        return request;
    }

    private static ProjectServiceAccountsAccessPoliciesRequestModel SetupValidRequest(
        ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        foreach (var policyRequest in request.ServiceAccountAccessPolicyRequests)
        {
            policyRequest.Read = true;
        }

        return request;
    }

    private static void SetupProjectAccessPoliciesTest(SutProvider<AccessPoliciesController> sutProvider, Project data,
        AccessClientType accessClientType)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>())
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .ReturnsForAnyArgs((true, true));
        sutProvider.GetDependency<IAccessClientQuery>()
            .GetAccessClientAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>())
            .ReturnsForAnyArgs((accessClientType, Guid.NewGuid()));
    }

    private static void SetupSecretAccessPoliciesTest(SutProvider<AccessPoliciesController> sutProvider, Secret data)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(data.Id).Returns(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(Guid.NewGuid());
    }
}
