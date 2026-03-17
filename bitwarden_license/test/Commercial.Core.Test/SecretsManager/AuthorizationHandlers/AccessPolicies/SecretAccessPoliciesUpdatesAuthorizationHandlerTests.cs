using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
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
public class SecretAccessPoliciesUpdatesAuthorizationHandlerTests
{
    [Fact]
    public void SecretAccessPoliciesOperations_OnlyPublicStatic()
    {
        var publicStaticFields =
            typeof(SecretAccessPoliciesOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(SecretAccessPoliciesOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_AccessSecretsManagerFalse_DoesNotSucceed(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
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
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, accessClientType, resource);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedServiceAccountGrantedPoliciesOperationRequirement_Throws(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new SecretAccessPoliciesOperationRequirement();
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
    public async Task Handler_CanUpdateAsync_UserHasNoWriteAccessToSecret_DoesNotSucceed(
        AccessClientType accessClientType,
        bool readAccess,
        bool writeAccess,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretAsync(resource.SecretId, userId, accessClientType)
            .Returns((readAccess, writeAccess, false));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(true, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(true, false, true)]
    [BitAutoData(false, true, true)]
    public async Task Handler_CanUpdateAsync_TargetGranteesNotInSameOrganization_DoesNotSucceed(
        bool orgUsersInSameOrg,
        bool groupsInSameOrg,
        bool serviceAccountsInSameOrg,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        SetupSameOrganizationRequest(sutProvider, AccessClientType.NoAccessCheck, resource, userId, orgUsersInSameOrg,
            groupsInSameOrg, serviceAccountsInSameOrg);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(true, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(true, false, true)]
    [BitAutoData(false, true, true)]
    public async Task Handler_CanUpdateAsync_TargetGranteesNotInSameOrganizationHasZeroRequests_DoesNotSucceed(
        bool orgUsersCountZero,
        bool groupsCountZero,
        bool serviceAccountsCountZero,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = ClearAccessPolicyUpdate(resource, orgUsersCountZero, groupsCountZero, serviceAccountsCountZero);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.NoAccessCheck, resource, userId, false, false,
            false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanUpdateAsync_NoServiceAccountCreatesRequested_Success(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;

        resource = RemoveAllServiceAccountCreates(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanUpdateAsync_NoAccessToTargetServiceAccounts_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;

        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupNoServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanUpdateAsync_ServiceAccountAccessResultsPartial_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupPartialServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanUpdateAsync_UserHasAccessToSomeServiceAccounts_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupSomeServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanUpdateAsync_UserHasAccessToAllServiceAccounts_Success(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddServiceAccountCreateUpdate(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupAllServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanCreateAsync_NotCreationOperations_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck, false, false)]
    [BitAutoData(AccessClientType.NoAccessCheck, true, false)]
    [BitAutoData(AccessClientType.User, false, false)]
    [BitAutoData(AccessClientType.User, true, false)]
    public async Task Handler_CanCreateAsync_UserHasNoManageAccessToSecret_DoesNotSucceed(
        AccessClientType accessClientType,
        bool readAccess,
        bool writeAccess,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);
        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretAsync(resource.SecretId, userId, accessClientType)
            .Returns((readAccess, writeAccess, false));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(true, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(true, false, true)]
    [BitAutoData(false, true, true)]
    public async Task Handler_CanCreateAsync_TargetGranteesNotInSameOrganization_DoesNotSucceed(
        bool orgUsersInSameOrg,
        bool groupsInSameOrg,
        bool serviceAccountsInSameOrg,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.NoAccessCheck, resource, userId, orgUsersInSameOrg,
            groupsInSameOrg, serviceAccountsInSameOrg);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, false, false)]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(true, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(true, false, true)]
    [BitAutoData(false, true, true)]
    public async Task Handler_CanCreateAsync_TargetGranteesNotInSameOrganizationHasZeroRequests_DoesNotSucceed(
        bool orgUsersCountZero,
        bool groupsCountZero,
        bool serviceAccountsCountZero,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        resource = ClearAccessPolicyUpdate(resource, orgUsersCountZero, groupsCountZero, serviceAccountsCountZero);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.NoAccessCheck, resource, userId, false, false,
            false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanCreateAsync_NoServiceAccountCreatesRequested_Success(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        resource = RemoveAllServiceAccountCreates(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanCreateAsync_NoAccessToTargetServiceAccounts_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupNoServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanCreateAsync_ServiceAccountAccessResultsPartial_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        resource = AddServiceAccountCreateUpdate(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupPartialServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanCreateAsync_UserHasAccessToSomeServiceAccounts_DoesNotSucceed(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        resource = AddServiceAccountCreateUpdate(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupSomeServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessClientType.NoAccessCheck)]
    [BitAutoData(AccessClientType.User)]
    public async Task Handler_CanCreateAsync_UserHasAccessToAllServiceAccounts_Success(
        AccessClientType accessClientType,
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Create;
        resource = SetAllToCreates(resource);
        resource = AddServiceAccountCreateUpdate(resource);
        SetupSameOrganizationRequest(sutProvider, accessClientType, resource, userId);
        SetupAllServiceAccountAccess(sutProvider, resource, userId, accessClientType);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_CanUpdateAsync_SA_ManageGrant_IsCreator_Success(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddManageServiceAccountUpdate(resource, userId);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.ServiceAccount, resource, userId);
        SetupAllServiceAccountAccess(sutProvider, resource, userId, AccessClientType.ServiceAccount);
        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdBySecretIdAsync(resource.SecretId)
            .Returns(userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_CanUpdateAsync_SA_ManageGrant_NotCreator_DoesNotSucceed(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddManageServiceAccountUpdate(resource, userId);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.ServiceAccount, resource, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdBySecretIdAsync(resource.SecretId)
            .Returns(Guid.NewGuid());
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_CanUpdateAsync_SA_ManageGrant_NullCreator_DoesNotSucceed(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddManageServiceAccountUpdate(resource, userId);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.ServiceAccount, resource, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdBySecretIdAsync(resource.SecretId)
            .Returns((Guid?)null);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_CanUpdateAsync_SA_NoManageGrant_DoesNotCheckCreator_Success(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = RemoveAllServiceAccountCreates(resource);
        resource = ClearAllManageFlags(resource);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.ServiceAccount, resource, userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
        await sutProvider.GetDependency<IProjectRepository>()
            .DidNotReceive()
            .GetProjectCreatorServiceAccountIdBySecretIdAsync(Arg.Any<Guid>());
    }

    private static void SetupNoServiceAccountAccess(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        AccessClientType accessClientType)
    {
        var createServiceAccountIds = resource.ServiceAccountAccessPolicyUpdates
            .Where(ap => ap.Operation == AccessPolicyOperation.Create)
            .Select(uap => uap.AccessPolicy.ServiceAccountId!.Value)
            .ToList();
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(createServiceAccountIds.ToDictionary(id => id, _ => (false, false, false)));
    }

    private static void SetupPartialServiceAccountAccess(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        AccessClientType accessClientType)
    {
        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (true, true, false));
        accessResult[accessResult.First().Key] = (true, true, false);
        accessResult.Remove(accessResult.Last().Key);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);
    }

    private static void SetupSomeServiceAccountAccess(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        AccessClientType accessClientType)
    {
        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (false, false, false));

        accessResult[accessResult.First().Key] = (true, true, false);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);
    }

    private static void SetupAllServiceAccountAccess(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        AccessClientType accessClientType)
    {
        var accessResult = resource.ServiceAccountAccessPolicyUpdates
            .Where(x => x.Operation == AccessPolicyOperation.Create)
            .Select(x => x.AccessPolicy.ServiceAccountId!.Value)
            .ToDictionary(id => id, _ => (true, true, true));
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountsAsync(Arg.Any<List<Guid>>(), userId, accessClientType)
            .Returns(accessResult);
    }

    private static void SetupUserSubstitutes(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        SecretAccessPoliciesUpdates resource,
        Guid userId = new())
    {
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<IAccessClientQuery>().GetAccessClientAsync(default, resource.OrganizationId)
            .ReturnsForAnyArgs((accessClientType, userId));
    }

    private static void SetupSameOrganizationRequest(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        AccessClientType accessClientType,
        SecretAccessPoliciesUpdates resource,
        Guid userId = new(),
        bool orgUsersInSameOrg = true,
        bool groupsInSameOrg = true,
        bool serviceAccountsInSameOrg = true)
    {
        SetupUserSubstitutes(sutProvider, accessClientType, resource, userId);

        sutProvider.GetDependency<ISecretRepository>()
            .AccessToSecretAsync(resource.SecretId, userId, accessClientType)
            .Returns((true, true, true));

        sutProvider.GetDependency<ISameOrganizationQuery>()
            .OrgUsersInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(orgUsersInSameOrg);
        sutProvider.GetDependency<ISameOrganizationQuery>()
            .GroupsInTheSameOrgAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(groupsInSameOrg);
        sutProvider.GetDependency<IServiceAccountRepository>()
            .ServiceAccountsAreInOrganizationAsync(Arg.Any<List<Guid>>(), resource.OrganizationId)
            .Returns(serviceAccountsInSameOrg);
    }

    private static SecretAccessPoliciesUpdates RemoveAllServiceAccountCreates(
        SecretAccessPoliciesUpdates resource)
    {
        resource.ServiceAccountAccessPolicyUpdates =
            resource.ServiceAccountAccessPolicyUpdates.Where(x => x.Operation != AccessPolicyOperation.Create);
        return resource;
    }

    private static SecretAccessPoliciesUpdates SetAllToCreates(
        SecretAccessPoliciesUpdates resource)
    {
        resource.UserAccessPolicyUpdates = resource.UserAccessPolicyUpdates.Select(x =>
        {
            x.Operation = AccessPolicyOperation.Create;
            return x;
        });
        resource.GroupAccessPolicyUpdates = resource.GroupAccessPolicyUpdates.Select(x =>
        {
            x.Operation = AccessPolicyOperation.Create;
            return x;
        });
        resource.ServiceAccountAccessPolicyUpdates = resource.ServiceAccountAccessPolicyUpdates.Select(x =>
        {
            x.Operation = AccessPolicyOperation.Create;
            return x;
        });

        return resource;
    }

    private static SecretAccessPoliciesUpdates AddServiceAccountCreateUpdate(
        SecretAccessPoliciesUpdates resource)
    {
        resource.ServiceAccountAccessPolicyUpdates = resource.ServiceAccountAccessPolicyUpdates.Append(
            new ServiceAccountSecretAccessPolicyUpdate
            {
                AccessPolicy = new ServiceAccountSecretAccessPolicy
                {
                    ServiceAccountId = Guid.NewGuid(),
                    GrantedSecretId = resource.SecretId,
                    Read = true,
                    Write = true
                }
            });
        return resource;
    }

    private static SecretAccessPoliciesUpdates ClearAccessPolicyUpdate(SecretAccessPoliciesUpdates resource,
        bool orgUsersCountZero,
        bool groupsCountZero,
        bool serviceAccountsCountZero)
    {
        if (orgUsersCountZero)
        {
            resource.UserAccessPolicyUpdates = [];
        }

        if (groupsCountZero)
        {
            resource.GroupAccessPolicyUpdates = [];
        }

        if (serviceAccountsCountZero)
        {
            resource.ServiceAccountAccessPolicyUpdates = [];
        }

        return resource;
    }

    private static SecretAccessPoliciesUpdates AddManageServiceAccountUpdate(
        SecretAccessPoliciesUpdates resource, Guid serviceAccountId)
    {
        resource.ServiceAccountAccessPolicyUpdates = resource.ServiceAccountAccessPolicyUpdates.Append(
            new ServiceAccountSecretAccessPolicyUpdate
            {
                Operation = AccessPolicyOperation.Create,
                AccessPolicy = new ServiceAccountSecretAccessPolicy
                {
                    ServiceAccountId = serviceAccountId,
                    GrantedSecretId = resource.SecretId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            });
        return resource;
    }

    private static SecretAccessPoliciesUpdates ClearAllManageFlags(SecretAccessPoliciesUpdates resource)
    {
        foreach (var u in resource.UserAccessPolicyUpdates) u.AccessPolicy.Manage = false;
        foreach (var u in resource.GroupAccessPolicyUpdates) u.AccessPolicy.Manage = false;
        foreach (var u in resource.ServiceAccountAccessPolicyUpdates) u.AccessPolicy.Manage = false;
        return resource;
    }

    // H-1: SA Update with Manage — only the creator SA may escalate to Manage via Update
    private static SecretAccessPoliciesUpdates AddManageServiceAccountUpdateOperation(
        SecretAccessPoliciesUpdates resource, Guid serviceAccountId)
    {
        resource.ServiceAccountAccessPolicyUpdates = resource.ServiceAccountAccessPolicyUpdates.Append(
            new ServiceAccountSecretAccessPolicyUpdate
            {
                Operation = AccessPolicyOperation.Update,
                AccessPolicy = new ServiceAccountSecretAccessPolicy
                {
                    ServiceAccountId = serviceAccountId,
                    GrantedSecretId = resource.SecretId,
                    Read = true,
                    Write = true,
                    Manage = true
                }
            });
        return resource;
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_CanUpdateAsync_SA_ManageGrant_ViaUpdate_IsCreator_Succeeds(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddManageServiceAccountUpdateOperation(resource, userId);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.ServiceAccount, resource, userId);
        SetupAllServiceAccountAccess(sutProvider, resource, userId, AccessClientType.ServiceAccount);
        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdBySecretIdAsync(resource.SecretId)
            .Returns(userId);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.True(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_CanUpdateAsync_SA_ManageGrant_ViaUpdate_NotCreator_DoesNotSucceed(
        SutProvider<SecretAccessPoliciesUpdatesAuthorizationHandler> sutProvider,
        SecretAccessPoliciesUpdates resource,
        Guid userId,
        ClaimsPrincipal claimsPrincipal)
    {
        // H-1 regression: a SA that is NOT the creator must not escalate to Manage via Update.
        var requirement = SecretAccessPoliciesOperations.Updates;
        resource = AddManageServiceAccountUpdateOperation(resource, userId);
        SetupSameOrganizationRequest(sutProvider, AccessClientType.ServiceAccount, resource, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .GetProjectCreatorServiceAccountIdBySecretIdAsync(resource.SecretId)
            .Returns(Guid.NewGuid()); // different SA is the creator
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }
}
