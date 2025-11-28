using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
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
public class ProjectPeopleAccessPoliciesAuthorizationHandlerTests
{
    private static void SetupUserPermission(SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType, ProjectPeopleAccessPolicies resource, Guid userId = new(), bool read = true,
        bool write = true)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, resource.OrganizationId)
            .ReturnsForAnyArgs(
                (accessClientType, userId));
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(resource.Id, userId, accessClientType)
            .Returns((read, write));
    }

    private static void SetupOrganizationUsers(SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectPeopleAccessPolicies resource) =>
        sutProvider.GetDependency<ISameOrganizationQuery>()
            .OrgUsersInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(true);

    private static void SetupGroups(SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectPeopleAccessPolicies resource) =>
        sutProvider.GetDependency<ISameOrganizationQuery>()
            .GroupsInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(true);

    [Fact]
    public void PeopleAccessPoliciesOperations_OnlyPublicStatic()
    {
        var publicStaticFields =
            typeof(ProjectPeopleAccessPoliciesOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(ProjectPeopleAccessPoliciesOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedProjectPeopleAccessPoliciesOperationRequirement_Throws(
        SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider, ProjectPeopleAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ProjectPeopleAccessPoliciesOperationRequirement();
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, resource.OrganizationId)
            .ReturnsForAnyArgs(
                (AccessClientType.NoAccessCheck, new Guid()));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_AccessSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider, ProjectPeopleAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ProjectPeopleAccessPoliciesOperationRequirement();
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
    public async Task Handler_UnsupportedClientTypes_DoesNotSucceed(AccessClientType clientType,
        SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider, ProjectPeopleAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ProjectPeopleAccessPoliciesOperationRequirement();
        SetupUserPermission(sutProvider, clientType, resource);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    public async Task ReplaceProjectPeople_UserNotInOrg_DoesNotSucceed(AccessClientType accessClient,
        SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider, ProjectPeopleAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectPeopleAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId);
        sutProvider.GetDependency<ISameOrganizationQuery>()
            .OrgUsersInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    public async Task ReplaceProjectPeople_GroupNotInOrg_DoesNotSucceed(AccessClientType accessClient,
        SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider, ProjectPeopleAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectPeopleAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId);
        SetupOrganizationUsers(sutProvider, resource);

        sutProvider.GetDependency<ISameOrganizationQuery>()
            .GroupsInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId).Returns(false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User, false, false, false)]
    [BitAutoData(AccessClientType.User, false, true, true)]
    [BitAutoData(AccessClientType.User, true, false, false)]
    [BitAutoData(AccessClientType.User, true, true, true)]
    [BitAutoData(AccessClientType.NoAccessCheck, false, false, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, false, true, true)]
    [BitAutoData(AccessClientType.NoAccessCheck, true, false, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, true, true, true)]
    public async Task ReplaceProjectPeople_AccessCheck(AccessClientType accessClient, bool read, bool write,
        bool expected,
        SutProvider<ProjectPeopleAccessPoliciesAuthorizationHandler> sutProvider, ProjectPeopleAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectPeopleAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId, read, write);
        SetupOrganizationUsers(sutProvider, resource);
        SetupGroups(sutProvider, resource);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }
}
