#nullable enable
using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;
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
public class BulkSecretAuthorizationHandlerTests
{
    [Fact]
    public void BulkSecretOperations_OnlyPublicStatic()
    {
        var publicStaticFields = typeof(BulkSecretOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(BulkSecretOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_MisMatchedOrganizations_DoesNotSucceed(
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = BulkSecretOperations.ReadAll;
        resources[0].OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>())
            .ReturnsForAnyArgs(true);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_NoAccessToSecretsManager_DoesNotSucceed(
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = BulkSecretOperations.ReadAll;
        resources = SetSameOrganization(resources);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(Arg.Any<Guid>())
            .ReturnsForAnyArgs(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedSecretOperationRequirement_Throws(
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new BulkSecretOperationRequirement();
        resources = SetSameOrganization(resources);
        SetupUserSubstitutes(sutProvider, AccessClientType.User, resources.First().OrganizationId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.ServiceAccount)]
    public async Task Handler_NoAccessToSecrets_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = BulkSecretOperations.ReadAll;
        resources = SetSameOrganization(resources);
        var secretIds =
            SetupSecretAccessRequest(sutProvider, resources, accessClientType, resources.First().OrganizationId);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .Returns(secretIds.ToDictionary(id => id, _ => (false, false)));

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.ServiceAccount)]
    public async Task Handler_HasAccessToSomeSecrets_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = BulkSecretOperations.ReadAll;
        resources = SetSameOrganization(resources);
        var secretIds =
            SetupSecretAccessRequest(sutProvider, resources, accessClientType, resources.First().OrganizationId);

        var accessResult = secretIds.ToDictionary(secretId => secretId, _ => (false, false));
        accessResult[secretIds.First()] = (true, true);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .Returns(accessResult);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.ServiceAccount)]
    public async Task Handler_PartialAccessReturn_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = BulkSecretOperations.ReadAll;
        resources = SetSameOrganization(resources);
        var secretIds =
            SetupSecretAccessRequest(sutProvider, resources, accessClientType, resources.First().OrganizationId);

        var accessResult = secretIds.ToDictionary(secretId => secretId, _ => (false, false));
        accessResult.Remove(secretIds.First());
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .Returns(accessResult);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.User)]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.ServiceAccount)]
    public async Task Handler_HasAccessToAllSecrets_Success(
        AccessClientType accessClientType,
        SutProvider<BulkSecretAuthorizationHandler> sutProvider, List<Secret> resources,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = BulkSecretOperations.ReadAll;
        resources = SetSameOrganization(resources);
        var secretIds =
            SetupSecretAccessRequest(sutProvider, resources, accessClientType, resources.First().OrganizationId);

        var accessResult = secretIds.ToDictionary(secretId => secretId, _ => (true, true));
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>())
            .Returns(accessResult);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resources);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    private static List<Secret> SetSameOrganization(List<Secret> secrets)
    {
        var organizationId = secrets.First().OrganizationId;
        foreach (var secret in secrets)
        {
            secret.OrganizationId = organizationId;
        }

        return secrets;
    }

    private static void SetupUserSubstitutes(
        SutProvider<BulkSecretAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        Guid organizationId,
        Guid userId = new())
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, organizationId)
            .ReturnsForAnyArgs((accessClientType, userId));
    }

    private static List<Guid> SetupSecretAccessRequest(
        SutProvider<BulkSecretAuthorizationHandler> sutProvider,
        IEnumerable<Secret> resources,
        AccessClientType accessClientType,
        Guid organizationId,
        Guid userId = new())
    {
        SetupUserSubstitutes(sutProvider, accessClientType, organizationId, userId);
        return resources.Select(s => s.Id).ToList();
    }
}
