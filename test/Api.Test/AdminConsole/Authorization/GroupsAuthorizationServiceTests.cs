using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Authorization.Groups;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class GroupsAuthorizationServiceTests
{
    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_ReturnsThePostedCollectionsTheCallerCannotModify(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Guid organizationId, Guid groupId, Guid authorizedCollectionId, Guid unauthorizedCollectionId)
    {
        List<Guid> postedCollectionIds = [authorizedCollectionId, unauthorizedCollectionId];
        SetupGroupAccess(sutProvider, organizationId, postedCollectionIds, [authorizedCollectionId]);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, postedCollectionIds, [], []);

        Assert.Equal([unauthorizedCollectionId], result.UnauthorizedPostedCollectionIds);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_ReturnsTheCurrentCollectionsTheCallerCannotModify(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Guid organizationId, Guid groupId, Guid authorizedCollectionId, Guid unauthorizedCollectionId)
    {
        List<Guid> currentCollectionIds = [authorizedCollectionId, unauthorizedCollectionId];
        SetupGroupAccess(sutProvider, organizationId, currentCollectionIds, [authorizedCollectionId]);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, [], currentCollectionIds, []);

        Assert.Equal([unauthorizedCollectionId], result.UnauthorizedCurrentCollectionIds);
    }

    /// <summary>
    /// A collection that does not exist, or that belongs to another organization, is left out of the authorization
    /// service's result. The caller must see it as unauthorized rather than as silently ignored.
    /// </summary>
    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenAPostedCollectionDoesNotExist_ReportsItAsUnauthorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Guid organizationId, Guid groupId, Guid unknownCollectionId)
    {
        List<Guid> postedCollectionIds = [unknownCollectionId];
        SetupGroupAccess(sutProvider, organizationId, postedCollectionIds, []);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, postedCollectionIds, [], []);

        Assert.Equal([unknownCollectionId], result.UnauthorizedPostedCollectionIds);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenCreatingAGroup_CanAddSelf(
        SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId, Guid callerUserId)
    {
        SetupSelfAsPostedMember(sutProvider, organizationId, callerUserId,
            allowAdminAccessToAllCollectionItems: false, callerIsAlreadyInGroup: false, out var postedUserIds);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, null, [], [], postedUserIds);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenAddingSelfToAGroup_CannotAddSelf(
        SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId, Guid groupId, Guid callerUserId)
    {
        SetupSelfAsPostedMember(sutProvider, organizationId, callerUserId,
            allowAdminAccessToAllCollectionItems: false, callerIsAlreadyInGroup: false, out var postedUserIds);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, [], [], postedUserIds);

        Assert.False(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenAlreadyInTheGroup_CanAddSelf(
        SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId, Guid groupId, Guid callerUserId)
    {
        SetupSelfAsPostedMember(sutProvider, organizationId, callerUserId,
            allowAdminAccessToAllCollectionItems: false, callerIsAlreadyInGroup: true, out var postedUserIds);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, [], [], postedUserIds);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenAdminAccessToAllCollectionItemsIsAllowed_CanAddSelf(
        SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId, Guid groupId, Guid callerUserId)
    {
        SetupSelfAsPostedMember(sutProvider, organizationId, callerUserId,
            allowAdminAccessToAllCollectionItems: true, callerIsAlreadyInGroup: false, out var postedUserIds);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, [], [], postedUserIds);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenTheCallerIsNotAnOrganizationMember_CanAddSelf(
        SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId, Guid groupId, Guid callerUserId)
    {
        SetupOrganizationAbility(sutProvider, organizationId, allowAdminAccessToAllCollectionItems: false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(callerUserId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callerUserId).ReturnsNull();

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, [], [], [Guid.NewGuid()]);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeSaveAsync_WhenNotAddingSelf_CanAddSelf(
        SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId, Guid groupId, Guid callerUserId,
        OrganizationUser callerOrganizationUser, Guid otherOrganizationUserId)
    {
        SetupOrganizationAbility(sutProvider, organizationId, allowAdminAccessToAllCollectionItems: false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(callerUserId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callerUserId).Returns(callerOrganizationUser);

        var result = await sutProvider.Sut.AuthorizeSaveAsync(organizationId, groupId, [], [],
            [otherOrganizationUserId]);

        Assert.True(result.CanAddSelfToGroup);
        await sutProvider.GetDependency<IGroupRepository>().DidNotReceiveWithAnyArgs()
            .GetManyUserIdsByIdAsync(default);
    }

    private static void SetupGroupAccess(SutProvider<GroupsAuthorizationService> sutProvider, Guid organizationId,
        IReadOnlyCollection<Guid> requestedCollectionIds, HashSet<Guid> authorizedCollectionIds) =>
        sutProvider.GetDependency<ICollectionAuthorizationService>()
            .AuthorizeModifyGroupAccessManyAsync(organizationId,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(requestedCollectionIds)))
            .Returns(authorizedCollectionIds);

    private static void SetupOrganizationAbility(SutProvider<GroupsAuthorizationService> sutProvider,
        Guid organizationId, bool allowAdminAccessToAllCollectionItems) =>
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility
            {
                Id = organizationId,
                AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems,
            });

    private static void SetupSelfAsPostedMember(SutProvider<GroupsAuthorizationService> sutProvider,
        Guid organizationId, Guid callerUserId, bool allowAdminAccessToAllCollectionItems,
        bool callerIsAlreadyInGroup, out List<Guid> postedUserIds)
    {
        var callerOrganizationUser = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId };
        postedUserIds = [callerOrganizationUser.Id];

        SetupOrganizationAbility(sutProvider, organizationId, allowAdminAccessToAllCollectionItems);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(callerUserId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, callerUserId).Returns(callerOrganizationUser);
        sutProvider.GetDependency<IGroupRepository>().GetManyUserIdsByIdAsync(Arg.Any<Guid>())
            .Returns(callerIsAlreadyInGroup ? [callerOrganizationUser.Id] : new List<Guid>());
    }
}
