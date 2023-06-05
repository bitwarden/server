using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.ServiceAccounts;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.ServiceAccounts;

[SutProviderCustomize]
public class ServiceAccountAuthorizationHandlerTests
{
    private static void SetupPermission(SutProvider<ServiceAccountAuthorizationHandler> sutProvider,
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
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    [Fact]
    public void ServiceAccountOperations_OnlyPublicStatic()
    {
        var publicStaticFields = typeof(ServiceAccountOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(ServiceAccountOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedServiceAccountOperationRequirement_Throws(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(true);
        var requirement = new ServiceAccountOperationRequirement();
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_SupportedServiceAccountOperationRequirement_DoesNotThrow(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(true);

        var requirements = typeof(ServiceAccountOperations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(i => (ServiceAccountOperationRequirement)i.GetValue(null));

        foreach (var req in requirements)
        {
            var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { req },
                claimsPrincipal, serviceAccount);

            await sutProvider.Sut.HandleAsync(authzContext);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreateServiceAccount_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(false);
        var requirement = ServiceAccountOperations.Create;
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.ServiceAccount)]
    [BitAutoData(AccessClientType.Organization)]
    public async Task CanCreateServiceAccount_NotSupportedClientTypes_DoesNotSucceed(AccessClientType clientType,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountOperations.Create;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(serviceAccount.OrganizationId)
            .Returns(false);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, serviceAccount.OrganizationId)
            .ReturnsForAnyArgs(
                (clientType, new Guid()));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin)]
    [BitAutoData(PermissionType.RunAsUserWithPermission)]
    public async Task CanCreateServiceAccount_Success(PermissionType permissionType,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountOperations.Create;
        SetupPermission(sutProvider, permissionType, serviceAccount.OrganizationId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateServiceAccount_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountOperations.Update;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanUpdateServiceAccount_NullResource_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Update;
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, serviceAccount.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, null);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    public async Task CanUpdateServiceAccount_ShouldNotSucceed(PermissionType permissionType, bool read, bool write,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Update;
        SetupPermission(sutProvider, permissionType, serviceAccount.OrganizationId, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(serviceAccount.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, false, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task CanUpdateServiceAccount_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Update;
        SetupPermission(sutProvider, permissionType, serviceAccount.OrganizationId, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(serviceAccount.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }


    [Theory]
    [BitAutoData]
    public async Task CanReadServiceAccount_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountOperations.Read;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanReadServiceAccount_NullResource_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Read;
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, serviceAccount.OrganizationId, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, null);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true)]
    public async Task CanReadServiceAccount_ShouldNotSucceed(PermissionType permissionType, bool read, bool write,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Read;
        SetupPermission(sutProvider, permissionType, serviceAccount.OrganizationId, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(serviceAccount.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true)]
    [BitAutoData(PermissionType.RunAsAdmin, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false)]
    public async Task CanReadServiceAccount_Success(PermissionType permissionType, bool read, bool write,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Read;
        SetupPermission(sutProvider, permissionType, serviceAccount.OrganizationId, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(serviceAccount.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanDeleteServiceAccount_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = ServiceAccountOperations.Delete;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(serviceAccount.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanDeleteServiceAccount_NullResource_DoesNotSucceed(
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Delete;
        SetupPermission(sutProvider, PermissionType.RunAsAdmin, serviceAccount.OrganizationId, userId);
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
    public async Task CanDeleteProject_AccessCheck(PermissionType permissionType, bool read, bool write,
        bool expected,
        SutProvider<ServiceAccountAuthorizationHandler> sutProvider, ServiceAccount serviceAccount,
        ClaimsPrincipal claimsPrincipal,
        Guid userId)
    {
        var requirement = ServiceAccountOperations.Delete;
        SetupPermission(sutProvider, permissionType, serviceAccount.OrganizationId, userId);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(serviceAccount.Id, userId, Arg.Any<AccessClientType>())
            .Returns((read, write));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, serviceAccount);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }
}
