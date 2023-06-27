using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;
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

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.Secrets;

[SutProviderCustomize]
[ProjectCustomize]
public class SecretAuthorizationHandlerTests
{
    private static void SetupPermission(SutProvider<SecretAuthorizationHandler> sutProvider,
        PermissionType permissionType, Guid organizationId, Guid userId = new(),
        AccessClientType clientType = AccessClientType.User)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);

        sutProvider.GetDependency<IProjectRepository>().ProjectsAreInOrganization(default, default)
            .ReturnsForAnyArgs(true);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId).ReturnsForAnyArgs(
                    (AccessClientType.NoAccessCheck, userId));
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId).ReturnsForAnyArgs(
                    (clientType, userId));
                break;
            case PermissionType.RunAsServiceAccountWithPermission:
                sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId).ReturnsForAnyArgs(
                    (AccessClientType.ServiceAccount, userId));
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
    [BitAutoData(AccessClientType.Organization)]
    public async Task CanCreateSecret_NotSupportedClientTypes_DoesNotSucceed(AccessClientType clientType,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret, Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId, clientType);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, Arg.Any<AccessClientType>()).Returns(
                (true, true));
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
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, false)]
    public async Task CanCreateSecret_DoesNotSucceed(PermissionType permissionType, bool read, bool write,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, Arg.Any<AccessClientType>()).ReturnsForAnyArgs(
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
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, true)]
    public async Task CanCreateSecret_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Create;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, Arg.Any<AccessClientType>()).ReturnsForAnyArgs(
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
    [BitAutoData(AccessClientType.Organization)]
    public async Task CanUpdateSecret_NotSupportedClientTypes_DoesNotSucceed(AccessClientType clientType,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret, Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, PermissionType.RunAsUserWithPermission, secret.OrganizationId, userId, clientType);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, Arg.Any<AccessClientType>()).Returns(
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
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, true, true, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, true, false, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, true, true, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, true, false, false)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, false, true, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, false, false, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, false, true, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, false, false, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, false, false, false)]
    public async Task CanUpdateSecret_DoesNotSucceed(PermissionType permissionType, bool read, bool write,
        bool projectRead, bool projectWrite,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, Arg.Any<AccessClientType>()).Returns(
            (read, write));
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, Arg.Any<AccessClientType>()).Returns(
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
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, true)]
    public async Task CanUpdateSecret_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Update;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<ISecretRepository>().AccessToSecretAsync(secret.Id, userId, Arg.Any<AccessClientType>()).Returns(
            (read, write));
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(secret.Projects!.FirstOrDefault()!.Id, userId, Arg.Any<AccessClientType>()).Returns(
                (read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanDeleteSecret_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretOperations.Delete;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(secret.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanDeleteSecret_NullResource_DoesNotSucceed(
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = SecretOperations.Delete;
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, secret.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, null);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, false, false)]                                                                                                                                             
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, false, true, true)]                                                                                                                                               
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, false, false)]                                                                                                                                              
    [BitAutoData(PermissionType.RunAsServiceAccountWithPermission, true, true, true)]
    public async Task CanDeleteProject_AccessCheck(PermissionType permissionType, bool read, bool write,
        bool expected,
        SutProvider<SecretAuthorizationHandler> sutProvider, Secret secret,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = SecretOperations.Delete;
        SetupPermission(sutProvider, permissionType, secret.OrganizationId, userId);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretAsync(secret.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, secret);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }
}
