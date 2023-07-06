using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.Projects;

[SutProviderCustomize]
[ProjectCustomize]
public class ProjectAuthorizationHandlerTests
{
    private static void SetupPermission(SutProvider<ProjectAuthorizationHandler> sutProvider,
        PermissionType permissionType, Guid organizationId, Guid userId = new())
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId)
                    .ReturnsForAnyArgs(
                        (AccessClientType.NoAccessCheck, userId));
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId)
                    .ReturnsForAnyArgs(
                        (AccessClientType.User, userId));
                break;
            case PermissionType.RunAsServiceAccountWithPermission:
                sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId)
                    .ReturnsForAnyArgs(
                        (AccessClientType.ServiceAccount, userId));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    [Fact]
    public void ProjectOperations_OnlyPublicStatic()
    {
        var publicStaticFields = typeof(ProjectOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(ProjectOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedProjectOperationRequirement_Throws(
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(true);
        var requirement = new ProjectOperationRequirement();
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SupportedProjectOperationRequirement_DoesNotThrow(
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(true);

        var requirements = typeof(ProjectOperations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(i => (ProjectOperationRequirement)i.GetValue(null));

        foreach (var req in requirements)
        {
            var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { req },
                claimsPrincipal, project);

            await sutProvider.Sut.HandleAsync(authzContext);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreateProject_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(false);
        var requirement = ProjectOperations.Create;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.Organization)]
    public async Task CanCreateProject_NotSupportedClientTypes_DoesNotSucceed(AccessClientType clientType,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, project.OrganizationId)
            .ReturnsForAnyArgs(
                (clientType, new Guid()));
        var requirement = ProjectOperations.Create;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission)]
    public async Task CanCreateProject_Success(PermissionType permissionType,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        SetupPermission(sutProvider, permissionType, project.OrganizationId);
        var requirement = ProjectOperations.Create;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }


    [Theory]
    [BitAutoData]
    public async Task CanUpdateProject_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(false);
        var requirement = ProjectOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateProject_NullResource_DoesNotSucceed(
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(true);
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, project.OrganizationId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(project.Id, userId, Arg.Any<AccessClientType>())
            .Returns((true, true));
        var requirement = ProjectOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, null);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateProject_NotSupportedClientType_DoesNotSucceed(
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(false);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, project.OrganizationId)
            .ReturnsForAnyArgs(
                (AccessClientType.ServiceAccount, new Guid()));
        var requirement = ProjectOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, false)]
    public async Task CanUpdateProject_ShouldNotSucceed(PermissionType permissionType, bool read, bool write,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        SetupPermission(sutProvider, permissionType, project.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(project.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var requirement = ProjectOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, true)]
    public async Task CanUpdateProject_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        SetupPermission(sutProvider, permissionType, project.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(project.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var requirement = ProjectOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }
}
