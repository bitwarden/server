using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
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

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.Secrets;

[SutProviderCustomize]
[ProjectCustomize]
public class SecretAuthorizationHandlerTests
{
    private static void SetupPermission(SutProvider<SecretAuthorizationHandler> sutProvider,
        PermissionType permissionType, Guid organizationId, Guid userId = new(),
        ClientType clientType = ClientType.User)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);

        sutProvider.GetDependency<IProjectRepository>().ProjectsAreInOrganization(default, default)
            .ReturnsForAnyArgs(true);

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

    [Fact]
    public void SecretOperations_OnlyPublicStatic()
    {
        var publicStaticFields = typeof(SecretOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(SecretOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedSecretOperationRequirement_Throws(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId)
            .Returns(true);
        var requirement = new SecretOperationRequirement();
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SupportedSecretOperationRequirement_Throws(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret, ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());
        var requirements = typeof(SecretOperations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(i => (SecretOperationRequirement)i.GetValue(null));

        foreach (var req in requirements)
        {
            var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { req },
                claimsPrincipal, secret);

            await sutProvider.Sut.HandleAsync(authzContext);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreateSecret_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId)
            .Returns(false);
        var requirement = SecretOperations.Create;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task CanCreateSecret_NotSupportedClientTypes_DoesNotSucceed(ClientType clientType,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret, Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId, clientType);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, default).Returns(
                (true, true));
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreateSecret_ProjectsNotInOrg_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>().ProjectsAreInOrganization(default, default)
            .ReturnsForAnyArgs(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreateSecret_WithoutProjectUser_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        secret.Projects = null;
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreateSecret_WithoutProjectAdmin_Success(SutProvider<SecretAuthorizationHandler> sutProvider,
        Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        secret.Projects = null;
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, secret.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task CanCreateSecret_DoesNotSucceed(PermissionType permissionType, bool read, bool write,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, default).Returns(
                (read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsAdmin, true, false)]
    [BitAutoData(PermissionType.RunAsAdmin, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task CanCreateSecret_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, default).Returns(
                (read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateSecret_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId)
            .Returns(false);
        var requirement = SecretOperations.Update;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task CanUpdateSecret_NotSupportedClientTypes_DoesNotSucceed(ClientType clientType,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret, Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId, clientType);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, default).Returns(
                (true, true));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateSecret_ProjectsNotInOrg_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>().ProjectsAreInOrganization(default, default)
            .ReturnsForAnyArgs(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateSecret_WithoutProjectUser_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        secret.Projects = null;
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateSecret_WithoutProjectAdmin_Success(SutProvider<SecretAuthorizationHandler> sutProvider,
        Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        secret.Projects = null;
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, secret.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, false, false)]
    public async Task CanUpdateSecret_DoesNotSucceed(PermissionType permissionType, bool read, bool write,
        bool projectRead, bool projectWrite,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default).Returns(
            (read, write));
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, default).Returns(
                (projectRead, projectWrite));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsAdmin, true, false)]
    [BitAutoData(PermissionType.RunAsAdmin, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task CanUpdateSecret_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, default).Returns(
            (read, write));
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, default).Returns(
                (read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }
}
