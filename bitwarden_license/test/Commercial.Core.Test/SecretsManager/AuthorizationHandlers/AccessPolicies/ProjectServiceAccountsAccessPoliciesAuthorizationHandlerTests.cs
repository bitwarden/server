#nullable enable
using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
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
public class ProjectServiceAccountsAccessPoliciesAuthorizationHandlerTests
{
    [Fact]
    public void ServiceAccountGrantedPoliciesOperations_OnlyPublicStatic()
    {
        var publicStaticFields =
            typeof(ProjectServiceAccountsAccessPoliciesOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(ProjectServiceAccountsAccessPoliciesOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_AccessSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
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
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, accessClientType, resource);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedProjectServiceAccountsPoliciesOperationRequirement_Throws(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new ProjectServiceAccountsAccessPoliciesOperationRequirement();
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
    public async Task Handler_UserHasNoManageAccessToProject_DoesNotSucceed(
        AccessClientType accessClientType,
        bool projectReadAccess,
        bool projectWriteAccess,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, accessClientType)
            .Returns((projectReadAccess, projectWriteAccess, false));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_ServiceAccountsInDifferentOrganization_DoesNotSucceed(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.NoAccessCheck, resource, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, AccessClientType.NoAccessCheck)
            .Returns((true, true, true));
        sutProvider.GetDependency<IServiceAccountRepository>()
            .ServiceAccountsAreInOrganizationAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasAccessToProject_NoCreatesRequested_Success(
        AccessClientType accessClientType,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        resource = RemoveAllCreates(resource);
        SetupServiceAccountsAccessTest(sutProvider, accessClientType, resource, userId);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasNoAccessToCreateServiceAccounts_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupServiceAccountsAccessTest(sutProvider, accessClientType, resource, userId);
        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (false, false, false));

        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
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
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupServiceAccountsAccessTest(sutProvider, accessClientType, resource, userId);

        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (false, false, false));

        accessResult[accessResult.First().Key] = (true, true, false);
        accessResult.Remove(accessResult.Last().Key);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);


        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasAccessToSomeCreateServiceAccounts_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupServiceAccountsAccessTest(sutProvider, accessClientType, resource, userId);

        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (false, false, false));

        accessResult[accessResult.First().Key] = (true, true, false);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);


        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_UserHasAccessToAllCreateServiceAccounts_Success(
        AccessClientType accessClientType,
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupServiceAccountsAccessTest(sutProvider, accessClientType, resource, userId);

        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (true, true, true));

        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);


        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    private static void SetupUserSubstitutes(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId = new())
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default!, resource.OrganizationId)
            .ReturnsForAnyArgs((accessClientType, userId));
    }

    private static void SetupServiceAccountsAccessTest(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId = new())
    {
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, accessClientType)
            .Returns((true, true, true));
        sutProvider.GetDependency<IServiceAccountRepository>()
            .ServiceAccountsAreInOrganizationAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(true);
    }

    private static ProjectServiceAccountsAccessPoliciesUpdates AddServiceAccountCreateUpdate(
        ProjectServiceAccountsAccessPoliciesUpdates resource)
    {
        resource.ServiceAccountAccessPolicyUpdates = resource.ServiceAccountAccessPolicyUpdates.Append(
            new ServiceAccountProjectAccessPolicyUpdate
            {
                AccessPolicy = new ServiceAccountProjectAccessPolicy
                {
                    ServiceAccountId = Guid.NewGuid(),
                    GrantedProjectId = resource.ProjectId,
                    Read = true,
                    Write = true
                }
            });
        return resource;
    }

    private static ProjectServiceAccountsAccessPoliciesUpdates RemoveAllCreates(
        ProjectServiceAccountsAccessPoliciesUpdates resource)
    {
        resource.ServiceAccountAccessPolicyUpdates =
            resource.ServiceAccountAccessPolicyUpdates.Where(x => x.Operation != AccessPolicyOperation.Create);
        return resource;
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SA_GrantManage_ProjectNotCreatedBySA_DoesNotSucceed(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.ServiceAccount, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, AccessClientType.ServiceAccount)
            .Returns((true, true, true));

        var otherSaId = Guid.NewGuid();
        resource.ServiceAccountAccessPolicyUpdates = resource.ServiceAccountAccessPolicyUpdates.Append(
            new ServiceAccountProjectAccessPolicyUpdate
            {
                Operation = AccessPolicyOperation.Create,
                AccessPolicy = new ServiceAccountProjectAccessPolicy
                {
                    ServiceAccountId = otherSaId,
                    GrantedProjectId = resource.ProjectId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            });

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdAsync(resource.ProjectId)
            .Returns(Guid.NewGuid());

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SA_GrantManage_ToItself_OnProjectItCreated_Succeeds(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.ServiceAccount, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, AccessClientType.ServiceAccount)
            .Returns((true, true, true));

        resource.ServiceAccountAccessPolicyUpdates = new List<ServiceAccountProjectAccessPolicyUpdate>
        {
            new()
            {
                Operation = AccessPolicyOperation.Create,
                AccessPolicy = new ServiceAccountProjectAccessPolicy
                {
                    ServiceAccountId = userId,
                    GrantedProjectId = resource.ProjectId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            }
        };

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdAsync(resource.ProjectId)
            .Returns(userId);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .ServiceAccountsAreInOrganizationAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(true);

        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, AccessClientType.ServiceAccount)
            .Returns(new Dictionary<Guid, (bool Read, bool Write, bool Manage)> { { userId, (true, true, true) } });

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SA_GrantManage_ToDifferentSA_EvenAsCreator_DoesNotSucceed(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.ServiceAccount, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, AccessClientType.ServiceAccount)
            .Returns((true, true, true));

        var otherSaId = Guid.NewGuid();
        resource.ServiceAccountAccessPolicyUpdates = new List<ServiceAccountProjectAccessPolicyUpdate>
        {
            new()
            {
                Operation = AccessPolicyOperation.Create,
                AccessPolicy = new ServiceAccountProjectAccessPolicy
                {
                    ServiceAccountId = otherSaId,
                    GrantedProjectId = resource.ProjectId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            }
        };

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdAsync(resource.ProjectId)
            .Returns(userId);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SA_GrantManage_ViaUpdateOperation_NotCreator_DoesNotSucceed(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        // Update op with Manage=true must trigger the delegation guard,
        // not bypass it by using a non-Create operation.
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.ServiceAccount, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, AccessClientType.ServiceAccount)
            .Returns((true, true, true));

        resource.ServiceAccountAccessPolicyUpdates = new List<ServiceAccountProjectAccessPolicyUpdate>
        {
            new()
            {
                Operation = AccessPolicyOperation.Update,
                AccessPolicy = new ServiceAccountProjectAccessPolicy
                {
                    ServiceAccountId = userId,
                    GrantedProjectId = resource.ProjectId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            }
        };

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdAsync(resource.ProjectId)
            .Returns(Guid.NewGuid());

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SA_GrantManage_NullCreatedByServiceAccountId_DoesNotSucceed(
        SutProvider<ProjectServiceAccountsAccessPoliciesAuthorizationHandler> sutProvider,
        ProjectServiceAccountsAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ProjectServiceAccountsAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, AccessClientType.ServiceAccount, resource, userId);

        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.ProjectId, userId, AccessClientType.ServiceAccount)
            .Returns((true, true, true));

        resource.ServiceAccountAccessPolicyUpdates = new List<ServiceAccountProjectAccessPolicyUpdate>
        {
            new()
            {
                Operation = AccessPolicyOperation.Create,
                AccessPolicy = new ServiceAccountProjectAccessPolicy
                {
                    ServiceAccountId = userId,
                    GrantedProjectId = resource.ProjectId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            }
        };

        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdAsync(resource.ProjectId)
            .Returns((Guid?)null);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }
}
