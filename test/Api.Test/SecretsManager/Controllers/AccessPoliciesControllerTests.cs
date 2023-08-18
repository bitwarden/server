using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.Test.SecretsManager.Enums;
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
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.NoAccessCheck)
                    .Returns((true, true));
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
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
    public async void GetProjectAccessPolicies_UserWithoutPermission_Throws(
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
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.NoAccessCheck)
                    .Returns((true, true));
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), AccessClientType.User)
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
    public async void GetProjectAccessPolicies_ProjectsExist_UserWithoutPermission_Throws(
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
            .GetManyByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>());

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
            .GetManyByGrantedServiceAccountIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
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

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedServiceAccountIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetServiceAccountAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByGrantedServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>());

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

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByGrantedServiceAccountIdAsync(default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountAccessPoliciesAsync(id));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByGrantedServiceAccountIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountGrantedPolicies_ReturnsEmptyList(
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

        var result = await sutProvider.Sut.GetServiceAccountGrantedPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async void GetServiceAccountGrantedPolicies_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount data,
        ServiceAccountProjectAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                SetupAdmin(sutProvider, data.OrganizationId);
                break;
            case PermissionType.RunAsUserWithPermission:
                SetupUserWithPermission(sutProvider, data.OrganizationId);
                break;
        }

        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByServiceAccountIdAsync(default, default, default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetServiceAccountGrantedPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByServiceAccountIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>(),
                Arg.Any<AccessClientType>());

        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void CreateProjectAccessPolicies_RequestMoreThanMax_Throws(
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
    public async void CreateProjectAccessPolicies_ProjectDoesNotExist_Throws(
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
    public async void CreateProjectAccessPolicies_DuplicatePolicy_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Project mockProject,
        UserProjectAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        var dup = new AccessPolicyRequest() { GranteeId = Guid.NewGuid(), Read = true, Write = true };
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
    public async void CreateProjectAccessPolicies_NoAccess_Throws(
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
    public async void CreateProjectAccessPolicies_Success(
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
    public async void CreateServiceAccountAccessPolicies_RequestMoreThanMax_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        UserServiceAccountAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        request = AddRequestsOverMax(request);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateServiceAccountAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountAccessPolicies_ServiceAccountDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        AccessPoliciesCreateRequest request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateServiceAccountAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountAccessPolicies_DuplicatePolicy_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        UserServiceAccountAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        var dup = new AccessPolicyRequest() { GranteeId = Guid.NewGuid(), Read = true, Write = true };
        request.UserAccessPolicyRequests = new[] { dup, dup };
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);

        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateServiceAccountAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountAccessPolicies_NoAccess_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        UserServiceAccountAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);
        var policies = request.ToBaseAccessPoliciesForServiceAccount(id, serviceAccount.OrganizationId);
        foreach (var policy in policies)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), policy,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        }

        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateServiceAccountAccessPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountAccessPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        UserServiceAccountAccessPolicy data,
        AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);
        var policies = request.ToBaseAccessPoliciesForServiceAccount(id, serviceAccount.OrganizationId);
        foreach (var policy in policies)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), policy,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }

        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await sutProvider.Sut.CreateServiceAccountAccessPoliciesAsync(id, request);

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().Received(1)
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountGrantedPolicies_RequestMoreThanMax_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        ServiceAccountProjectAccessPolicy data,
        List<GrantedAccessPolicyRequest> request)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        request = AddRequestsOverMax(request);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateServiceAccountGrantedPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountGrantedPolicies_ServiceAccountDoesNotExist_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        List<GrantedAccessPolicyRequest> request)
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateServiceAccountGrantedPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountGrantedPolicies_DuplicatePolicy_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        ServiceAccountProjectAccessPolicy data,
        List<GrantedAccessPolicyRequest> request)
    {
        var dup = new GrantedAccessPolicyRequest() { GrantedId = Guid.NewGuid(), Read = true, Write = true };
        request.Add(dup);
        request.Add(dup);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);

        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateServiceAccountGrantedPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountGrantedPolicies_NoAccess_Throws(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        ServiceAccountProjectAccessPolicy data,
        List<GrantedAccessPolicyRequest> request)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });
        foreach (var policy in request)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), policy,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());
        }

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.CreateServiceAccountGrantedPoliciesAsync(id, request));

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().DidNotReceiveWithAnyArgs()
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccountGrantedPolicies_Success(
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        ServiceAccount serviceAccount,
        ServiceAccountProjectAccessPolicy data,
        List<GrantedAccessPolicyRequest> request)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(serviceAccount);
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>()
            .CreateManyAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });
        foreach (var policy in request)
        {
            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), policy,
                    Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        }

        await sutProvider.Sut.CreateServiceAccountGrantedPoliciesAsync(id, request);

        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().Received(1)
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateAccessPolicies_NoAccess_Throws(
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
    public async void UpdateAccessPolicies_Success(
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
    public async void DeleteAccessPolicies_NoAccess_Throws(SutProvider<AccessPoliciesController> sutProvider, Guid id)
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
    public async void DeleteAccessPolicies_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id)
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
    public async void GetPeoplePotentialGrantees_ReturnsEmptyList(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        SetupPermission(sutProvider, permissionType, id);
        var result = await sutProvider.Sut.GetPeoplePotentialGranteesAsync(id);

        await sutProvider.GetDependency<IGroupRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetPeoplePotentialGrantees_UserWithoutPermission_Throws(
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
    public async void GetPeoplePotentialGrantees_Success(
        PermissionType permissionType,
        SutProvider<AccessPoliciesController> sutProvider,
        Guid id,
        Group mockGroup)
    {
        SetupPermission(sutProvider, permissionType, id);
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
    public async void GetServiceAccountsPotentialGrantees_ReturnsEmptyList(
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
    public async void GetProjectPotentialGrantees_ReturnsEmptyList(
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
    public async void GetProjectPotentialGrantees_UserWithoutPermission_Throws(
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
    public async void GetProjectPotentialGrantees_Success(
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
}
