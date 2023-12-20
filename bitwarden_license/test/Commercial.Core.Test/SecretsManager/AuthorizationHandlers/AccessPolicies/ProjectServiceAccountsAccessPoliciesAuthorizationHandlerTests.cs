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
public class ProjectServiceAccountAccessPoliciesAuthorizationHandlerTests
{
    private static void SetupUserPermission(SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType, ProjectServiceAccountsAccessPolicies resource, Guid userId = new(), bool read = true,
        bool write = true)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, resource.OrganizationId)
            .ReturnsForAnyArgs(
                (accessClientType, userId));
        sutProvider.GetDependency<IProjectRepository>().AccessToProjectAsync(resource.Id, userId, accessClientType)
            .Returns((read, write));
        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns((read, write));
    }

    private static void SetupOrganizationServiceAccounts(SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPolicies resource) =>
        sutProvider.GetDependency<ISameOrganizationQuery>()
            .ServiceAccountsInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(true);

    [Fact]
    public void ServiceAccountAccessPoliciesOperations_OnlyPublicStatic()
    {
        var publicStaticFields =
            typeof(ProjectServiceAccountsAccessPoliciesOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(ProjectServiceAccountsAccessPoliciesOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedProjectServiceAccountsAccessPoliciesOperationRequirement_Throws(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider, ProjectServiceAccountsAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ProjectServiceAccountsAccessPoliciesOperationRequirement();
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
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider, ProjectServiceAccountsAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ProjectServiceAccountsAccessPoliciesOperationRequirement();
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    public async Task ReplaceProjectServiceAccount_ServiceAccountNotInOrg_DoesNotSucceed(AccessClientType accessClient,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider, ProjectServiceAccountsAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId);
        sutProvider.GetDependency<ISameOrganizationQuery>()
            .ServiceAccountsInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(false);

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
    public async Task ReplaceProjectServiceAccount_ProjectAccessCheck(AccessClientType accessClient, bool read, bool write,
        bool expected,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider, ProjectServiceAccountsAccessPolicies resource,
        ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId, read, write);
        SetupOrganizationServiceAccounts(sutProvider, resource);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
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
    public async Task ReplaceProjectServiceAccount_ServiceAccountsAccessCheck(AccessClientType accessClient, bool read, bool write,
    bool expected,
    SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider, ProjectServiceAccountsAccessPolicies resource,
    ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId, true, true);
        SetupOrganizationServiceAccounts(sutProvider, resource);


        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClient).Returns((read, write));

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.ServiceAccount, false, false, false)]
    [BitAutoData(AccessClientType.ServiceAccount, false, true, false)]
    [BitAutoData(AccessClientType.ServiceAccount, true, false, false)]
    [BitAutoData(AccessClientType.ServiceAccount, true, true, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, false, false, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, false, true, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, true, false, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, true, true, false)]
    public async Task ReplaceProjectServiceAccount_ProjectAccessFalseCheck(AccessClientType accessClient, bool read, bool write,
    bool expected,
    SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider, ProjectServiceAccountsAccessPolicies resource,
    ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Replace;
        SetupUserPermission(sutProvider, accessClient, resource, userId, false, false);
        SetupOrganizationServiceAccounts(sutProvider, resource);

        sutProvider.GetDependency<IServiceAccountRepository>().AccessToServiceAccountsAsync(resource.ServiceAccountProjectsAccessPolicies.Select(ap => ap.ServiceAccountId!.Value).ToList(), userId, accessClient)
         .Returns((read, write));

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }

    //Test where we pass unsupported client types and it doesn't success. ServiceAccount/Organization.

}
