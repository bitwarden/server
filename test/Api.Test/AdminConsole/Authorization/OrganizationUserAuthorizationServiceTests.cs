using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Authorization.OrganizationUsers;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationUserAuthorizationServiceTests
{
    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_ReturnsThePostedCollectionsTheCallerCannotModify(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId, Guid organizationUserId, Guid authorizedCollectionId, Guid unauthorizedCollectionId)
    {
        List<Guid> postedCollectionIds = [authorizedCollectionId, unauthorizedCollectionId];
        SetupUserAccess(sutProvider, organizationId, postedCollectionIds, [authorizedCollectionId]);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, organizationUserId,
            postedCollectionIds, []);

        Assert.Equal([unauthorizedCollectionId], result.UnauthorizedPostedCollectionIds);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_ReturnsTheCurrentCollectionsTheCallerCannotModify(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId, Guid organizationUserId, Guid authorizedCollectionId, Guid unauthorizedCollectionId)
    {
        List<Guid> currentCollectionIds = [authorizedCollectionId, unauthorizedCollectionId];
        SetupUserAccess(sutProvider, organizationId, currentCollectionIds, [authorizedCollectionId]);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, organizationUserId, [],
            currentCollectionIds);

        Assert.Equal([unauthorizedCollectionId], result.UnauthorizedCurrentCollectionIds);
    }

    /// <summary>
    /// A collection that does not exist, or that belongs to another organization, is left out of the authorization
    /// service's result. The caller must see it as unauthorized rather than as silently ignored.
    /// </summary>
    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenAPostedCollectionDoesNotExist_ReportsItAsUnauthorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId, Guid organizationUserId, Guid unknownCollectionId)
    {
        List<Guid> postedCollectionIds = [unknownCollectionId];
        SetupUserAccess(sutProvider, organizationId, postedCollectionIds, []);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, organizationUserId,
            postedCollectionIds, []);

        Assert.Equal([unknownCollectionId], result.UnauthorizedPostedCollectionIds);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenInvitingAUser_CanAddSelfAndEditGroups(
        SutProvider<OrganizationUserAuthorizationService> sutProvider, Guid organizationId, Guid callerUserId,
        Guid postedCollectionId)
    {
        SetupOrganizationAbility(sutProvider, organizationId, allowAdminAccessToAllCollectionItems: false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(callerUserId);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, null, [postedCollectionId], []);

        Assert.True(result.CanAddSelfToCollection);
        Assert.True(result.CanEditOwnGroups);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenEditingSelfAndAddingANewCollection_CannotAddSelfToCollection(
        SutProvider<OrganizationUserAuthorizationService> sutProvider, Guid organizationId, Guid callerUserId,
        OrganizationUser targetOrganizationUser, Guid currentCollectionId, Guid newCollectionId)
    {
        SetupEditingSelf(sutProvider, organizationId, callerUserId, targetOrganizationUser,
            allowAdminAccessToAllCollectionItems: false);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, targetOrganizationUser.Id,
            [currentCollectionId, newCollectionId], [currentCollectionId]);

        Assert.False(result.CanAddSelfToCollection);
        Assert.False(result.CanEditOwnGroups);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenEditingSelfWithoutAddingACollection_CanAddSelfToCollection(
        SutProvider<OrganizationUserAuthorizationService> sutProvider, Guid organizationId, Guid callerUserId,
        OrganizationUser targetOrganizationUser, Guid currentCollectionId)
    {
        SetupEditingSelf(sutProvider, organizationId, callerUserId, targetOrganizationUser,
            allowAdminAccessToAllCollectionItems: false);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, targetOrganizationUser.Id,
            [currentCollectionId], [currentCollectionId]);

        Assert.True(result.CanAddSelfToCollection);
        // Editing your own membership still blocks group changes, even when no collection is added.
        Assert.False(result.CanEditOwnGroups);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenEditingSelfAndAdminAccessIsAllowed_CanAddSelfAndEditGroups(
        SutProvider<OrganizationUserAuthorizationService> sutProvider, Guid organizationId, Guid callerUserId,
        OrganizationUser targetOrganizationUser, Guid newCollectionId)
    {
        SetupEditingSelf(sutProvider, organizationId, callerUserId, targetOrganizationUser,
            allowAdminAccessToAllCollectionItems: true);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, targetOrganizationUser.Id,
            [newCollectionId], []);

        Assert.True(result.CanAddSelfToCollection);
        Assert.True(result.CanEditOwnGroups);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenEditingAnotherUser_CanAddSelfAndEditGroups(
        SutProvider<OrganizationUserAuthorizationService> sutProvider, Guid organizationId, Guid callerUserId,
        OrganizationUser targetOrganizationUser, Guid newCollectionId)
    {
        SetupOrganizationAbility(sutProvider, organizationId, allowAdminAccessToAllCollectionItems: false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(callerUserId);
        targetOrganizationUser.UserId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(targetOrganizationUser.Id)
            .Returns(targetOrganizationUser);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, targetOrganizationUser.Id,
            [newCollectionId], []);

        Assert.True(result.CanAddSelfToCollection);
        Assert.True(result.CanEditOwnGroups);
    }

    private static void SetupUserAccess(SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId, IReadOnlyCollection<Guid> requestedCollectionIds,
        HashSet<Guid> authorizedCollectionIds) =>
        sutProvider.GetDependency<ICollectionAuthorizationService>()
            .AuthorizeModifyUserAccessManyAsync(organizationId,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(requestedCollectionIds)))
            .Returns(authorizedCollectionIds);

    private static void SetupOrganizationAbility(SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId, bool allowAdminAccessToAllCollectionItems) =>
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility
            {
                Id = organizationId,
                AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems,
            });

    private static void SetupEditingSelf(SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId, Guid callerUserId, OrganizationUser targetOrganizationUser,
        bool allowAdminAccessToAllCollectionItems)
    {
        SetupOrganizationAbility(sutProvider, organizationId, allowAdminAccessToAllCollectionItems);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(callerUserId);
        targetOrganizationUser.UserId = callerUserId;
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(targetOrganizationUser.Id)
            .Returns(targetOrganizationUser);
    }
}
