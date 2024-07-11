#nullable enable
using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class ServiceAccountGrantedPoliciesAuthorizationHandlerTests
{
    [Fact]
    public void ServiceAccountGrantedPoliciesOperations_OnlyPublicStatic()
    {
        var publicStaticFields =
            typeof(ServiceAccountGrantedPoliciesOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(ServiceAccountGrantedPoliciesOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_AccessSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.ServiceAccount)]
    [BitAutoData(AccessClientType.Organization)]
    public async Task Handler_UnsupportedClientTypes_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, accessClientType, resource);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedServiceAccountGrantedPoliciesOperationRequirement_Throws(
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ServiceAccountGrantedPoliciesOperationRequirement();
        SetupUserSubstitutes(sutProvider, AccessClientType.NoAccessCheck, resource);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck, false, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, true, false)]
    [BitAutoData(AccessClientType.User, false, false)]
    [BitAutoData(AccessClientType.User, true, false)]
    public async Task Handler_UserHasNoWriteAccessToServiceAccount_DoesNotSucceed(
        AccessClientType accessClientType,
        bool saReadAccess,
        bool saWriteAccess,
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(resource.ServiceAccountId, userId, accessClientType)
            .Returns((saReadAccess, saWriteAccess));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_GrantedProjectsInDifferentOrganization_DoesNotSucceed(
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.NoAccessCheck, resource, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(resource.ServiceAccountId, userId, AccessClientType.NoAccessCheck)
            .Returns((true, true));
        sutProvider.GetDependency<IProjectRepository>()
            .ProjectsAreInOrganization(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasNoAccessToGrantedProjects_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        var projectIds = SetupProjectAccessTest(sutProvider, accessClientType, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(projectIds.ToDictionary(projectId => projectId, _ => (false, false)));


        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasAccessToSomeGrantedProjects_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        var projectIds = SetupProjectAccessTest(sutProvider, accessClientType, resource, userId);

        var accessResult = projectIds.ToDictionary(projectId => projectId, _ => (false, false));
        accessResult[projectIds.First()] = (true, true);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);


        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_AccessResultsPartial_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        var projectIds = SetupProjectAccessTest(sutProvider, accessClientType, resource, userId);

        var accessResult = projectIds.ToDictionary(projectId => projectId, _ => (false, false));
        accessResult.Remove(projectIds.First());
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasAccessToAllGrantedProjects_Success(
        AccessClientType accessClientType,
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountGrantedPoliciesOperations.Updates;
        var projectIds = SetupProjectAccessTest(sutProvider, accessClientType, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(projectIds.ToDictionary(projectId => projectId, _ => (true, true)));

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    private static void SetupUserSubstitutes(
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId = new())
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, resource.OrganizationId)
            .ReturnsForAnyArgs((accessClientType, userId));
    }

    private static List<Guid> SetupProjectAccessTest(
        SutProvider<ServiceAccountGrantedPoliciesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        ServiceAccountGrantedPoliciesUpdates resource,
        Guid userId = new())
    {
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(resource.ServiceAccountId, userId, accessClientType)
            .Returns((true, true));
        sutProvider.GetDependency<IProjectRepository>()
            .ProjectsAreInOrganization(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(true);

        return resource.ProjectGrantedPolicyUpdates
            .Select(pu => pu.AccessPolicy.GrantedProjectId!.Value)
            .ToList();
    }
}
