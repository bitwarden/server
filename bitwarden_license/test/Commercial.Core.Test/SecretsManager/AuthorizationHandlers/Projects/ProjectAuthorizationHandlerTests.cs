using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
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
        PermissionType permissionType, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
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
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task CanCreateProject_NotSupportedClientTypes_DoesNotSucceed(ClientType clientType,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(project.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);
        var requirement = ProjectOperations.Create;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
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
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
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
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.ServiceAccount);
        var requirement = ProjectOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, project);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task CanUpdateProject_ShouldNotSucceed(PermissionType permissionType, bool read, bool write,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        SetupPermission(sutProvider, permissionType, project.OrganizationId);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
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
    public async Task CanUpdateProject_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<ProjectAuthorizationHandler> sutProvider, Project project, ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        SetupPermission(sutProvider, permissionType, project.OrganizationId);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
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
