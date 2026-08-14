using Bit.Api.AdminConsole.Authorization.Groups;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
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
public class GroupsAuthorizationServiceTests
{
    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_GroupNotFound_AllUnauthorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Guid organizationId,
        Guid groupId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(null, new List<CollectionAccessSelection>()));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, groupId, [], []);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_GroupBelongsToDifferentOrganization_AllUnauthorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid organizationId,
        Guid groupId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, groupId, [], []);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_MissingUserId_AllUnauthorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [], []);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_SelfAddToGroup_NotAlreadyMember_Unauthorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        OrganizationUser callerOrganizationUser)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(group.OrganizationId, userId)
            .Returns(callerOrganizationUser);
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(groupId)
            .Returns(new List<Guid>());

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [], [callerOrganizationUser.Id]);

        Assert.False(result.CanAddSelfToGroup);
        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_SelfAddToGroup_AllowAdminAccessTrue_Authorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid postedUserId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(group.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [], [postedUserId]);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_SelfAlreadyMember_Authorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        OrganizationUser callerOrganizationUser)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(group.OrganizationId, userId)
            .Returns(callerOrganizationUser);
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyUserIdsByIdAsync(groupId)
            .Returns(new List<Guid> { callerOrganizationUser.Id });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [], [callerOrganizationUser.Id]);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_CallerIsProviderNotOrgUser_SelfAddCheckSkipped(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid postedUserId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(group.OrganizationId, userId)
            .Returns((OrganizationUser)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [], [postedUserId]);

        Assert.True(result.CanAddSelfToGroup);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_PostedCollectionUnauthorized_RejectsRequest(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid collectionId,
        Collection collection,
        CurrentContextOrganization organization)
    {
        collection.Id = collectionId;
        collection.OrganizationId = group.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(group.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [collectionId], []);

        Assert.Contains(collectionId, result.UnauthorizedPostedCollectionIds);
        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_CurrentCollectionUnauthorized_PreservedNotRejected(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid collectionId,
        Collection collection,
        CurrentContextOrganization organization)
    {
        collection.Id = collectionId;
        collection.OrganizationId = group.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var currentAccess = new List<CollectionAccessSelection> { new() { Id = collectionId } };

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, currentAccess));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(group.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [], []);

        Assert.Contains(collectionId, result.ReadonlyCurrentCollectionIds);
        Assert.Empty(result.UnauthorizedPostedCollectionIds);
        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_NonexistentPostedCollectionId_SilentlySkipped(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid collectionId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [collectionId], []);

        Assert.Empty(result.UnauthorizedPostedCollectionIds);
        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WhenCallerManagesCollection_Authorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid collectionId,
        Collection collection,
        CurrentContextOrganization organization)
    {
        collection.Id = collectionId;
        collection.OrganizationId = group.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(group.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = collectionId, Manage = true } });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [collectionId], []);

        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WhenProviderUser_FullyAuthorized(
        SutProvider<GroupsAuthorizationService> sutProvider,
        Group group,
        Guid groupId,
        Guid userId,
        Guid collectionId,
        Collection collection)
    {
        collection.Id = collectionId;
        collection.OrganizationId = group.OrganizationId;

        sutProvider.GetDependency<IGroupRepository>()
            .GetByIdWithCollectionsAsync(groupId)
            .Returns(new Tuple<Group, ICollection<CollectionAccessSelection>>(group, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(group.OrganizationId).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(group.OrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(group.OrganizationId, userId)
            .Returns((OrganizationUser)null);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(group.OrganizationId, groupId, [collectionId], []);

        Assert.True(result.IsSuccess);
    }
}
