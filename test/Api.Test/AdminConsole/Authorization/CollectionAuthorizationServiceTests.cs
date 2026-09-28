using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class CollectionAuthorizationServiceTests
{
    [Theory, BitAutoData]
    public async Task AllOperations_WhenCollectionNotFound_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        Guid userId)
    {
        SetupCollections(sutProvider);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        Assert.False(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WhenProviderUser_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        Guid userId)
    {
        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);

        Assert.True(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WithEditAnyCollectionPermission_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);

        Assert.True(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WhenCallerManagesCollection_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, Details(organizationId, collectionId, manage: true));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);

        Assert.True(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task AllOperations_WhenAssignedWithoutManage_NoSuccess(
        OrganizationUserType type,
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = type;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, Details(organizationId, collectionId, assigned: true));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        Assert.False(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WhenOwnerAndCollectionOrphaned_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Owner;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, Details(organizationId, collectionId, unmanaged: true));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });

        Assert.True(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Equal(Expected(collectionId), await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task AllOperations_WhenOwnerAndCollectionNotOrphaned_NoSuccess(
        OrganizationUserType type,
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = type;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        Assert.False(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WhenCollectionBelongsToDifferentOrganization_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid otherOrganizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Permissions = new Permissions { EditAnyCollection = true };

        SetupCollections(sutProvider, Details(otherOrganizationId, collectionId, manage: true));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);

        Assert.False(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WhenNotOrganizationMemberOrProvider_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        Guid userId)
    {
        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns((CurrentContextOrganization?)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(false);

        Assert.False(await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]));
        Assert.Empty(await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]));
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WhenMissingUserId_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId)
    {
        SetupCollections(sutProvider, Details(organizationId, collectionId, manage: true));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.False(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByIdsWithPermissionsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>(), Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WhenMissingPermissions_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task AuthorizeUpdateAsync_WhenAdminOrOwner_AllowAdminAccessTrue_Success(
        OrganizationUserType type,
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = type;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WithManageUsersPermission_WhenAllowAdminAccessTrue_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, collectionId);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeModifyUserAccessAsync_WhenEmptyRequest_ReturnsEmpty(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId)
    {
        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, []);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceive()
            .GetManyByIdsWithPermissionsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>(), Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task AuthorizeModifyUserAccessAsync_WithMixedAuthorization_ReturnsOnlyAuthorizedIds(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid authorizedCollectionId,
        Guid unauthorizedCollectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider,
            Details(organizationId, authorizedCollectionId, manage: true),
            Details(organizationId, unauthorizedCollectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(
            organizationId, [authorizedCollectionId, unauthorizedCollectionId]);

        Assert.Equal(Expected(authorizedCollectionId), result);
    }

    [Theory, BitAutoData]
    public async Task AllOperations_WhenCalledTwice_OnlyFetchesUncachedCollections(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid firstCollectionId,
        Guid secondCollectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        SetupCollections(sutProvider,
            Details(organizationId, firstCollectionId, manage: true),
            Details(organizationId, secondCollectionId, manage: true));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);

        var first = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [firstCollectionId]);
        var second = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(
            organizationId, [firstCollectionId, secondCollectionId]);

        Assert.Equal(Expected(firstCollectionId), first);
        Assert.Equal(Expected(firstCollectionId, secondCollectionId), second);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByIdsWithPermissionsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { firstCollectionId })),
            Arg.Any<Guid?>(), Arg.Any<bool>());
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByIdsWithPermissionsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { secondCollectionId })),
            Arg.Any<Guid?>(), Arg.Any<bool>());
    }

    [Theory, BitAutoData]
    public async Task AuthorizeModifyUserAccessAsync_WithManageUsersPermission_WhenAllowAdminAccessTrue_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]);

        Assert.Equal(Expected(collectionId), result);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeModifyUserAccessAsync_WithManageUsersPermission_WhenAllowAdminAccessFalse_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageUsers = true };

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyUserAccessAsync(organizationId, [collectionId]);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeModifyGroupAccessAsync_WithManageGroupsPermission_WhenAllowAdminAccessTrue_Success(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]);

        Assert.Equal(Expected(collectionId), result);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeModifyGroupAccessAsync_WithManageGroupsPermission_WhenAllowAdminAccessFalse_NoSuccess(
        SutProvider<CollectionAuthorizationService> sutProvider,
        Guid organizationId,
        Guid collectionId,
        CurrentContextOrganization organization,
        Guid userId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions { ManageGroups = true };

        SetupCollections(sutProvider, Details(organizationId, collectionId));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationId).Returns(organization);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var result = await sutProvider.Sut.AuthorizeModifyGroupAccessAsync(organizationId, [collectionId]);

        Assert.Empty(result);
    }

    private static void SetupCollections(
        SutProvider<CollectionAuthorizationService> sutProvider,
        params CollectionAdminDetails[] collections) =>
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByIdsWithPermissionsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>(), Arg.Any<bool>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(id => collections.FirstOrDefault(c => c.Id == id))
                .Where(c => c is not null)
                .ToList()!);

    private static CollectionAdminDetails Details(
        Guid organizationId,
        Guid collectionId,
        bool manage = false,
        bool assigned = false,
        bool unmanaged = false) =>
        new()
        {
            Id = collectionId,
            OrganizationId = organizationId,
            Manage = manage,
            Assigned = assigned || manage,
            Unmanaged = unmanaged
        };

    private static HashSet<Guid> Expected(params Guid[] collectionIds) => [.. collectionIds];
}
