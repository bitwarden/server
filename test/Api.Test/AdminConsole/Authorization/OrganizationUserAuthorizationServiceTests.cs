using Bit.Api.AdminConsole.Authorization.OrganizationUsers;
using Bit.Core.AdminConsole.AbilitiesCache;
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
public class OrganizationUserAuthorizationServiceTests
{
    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_OrganizationUserNotFound_AllUnauthorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        Guid organizationId,
        Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(null, new List<CollectionAccessSelection>()));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, organizationUserId, []);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_OrganizationUserBelongsToDifferentOrganization_AllUnauthorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationId,
        Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationId, organizationUserId, []);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_MissingUserId_AllUnauthorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, []);

        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_EditingSelf_AddingNewCollection_Unauthorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid newCollectionId)
    {
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(newCollectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [newCollectionId]);

        Assert.False(result.CanAddSelfToCollection);
        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_EditingSelf_OnlyPostingCurrentCollections_Authorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid collectionId)
    {
        organizationUser.UserId = userId;
        var currentAccess = new List<CollectionAccessSelection> { new() { Id = collectionId } };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, currentAccess));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [collectionId]);

        Assert.True(result.CanAddSelfToCollection);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_EditingSelf_AllowAdminAccessTrue_CanAddSelfToNewCollection(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid newCollectionId)
    {
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationUser.OrganizationId)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(newCollectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [newCollectionId]);

        Assert.True(result.CanAddSelfToCollection);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_NotEditingSelf_CanAddToNewCollectionRegardless(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId,
        Guid newCollectionId)
    {
        organizationUser.UserId = otherUserId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(newCollectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [newCollectionId]);

        Assert.True(result.CanAddSelfToCollection);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_EditingSelf_AllowAdminAccessFalse_CannotEditOwnGroups(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId)
    {
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, []);

        Assert.False(result.CanEditOwnGroups);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_NotEditingSelf_CanEditOwnGroups(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId)
    {
        organizationUser.UserId = otherUserId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, []);

        Assert.True(result.CanEditOwnGroups);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_PostedCollectionUnauthorized_RejectsRequest(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId,
        Guid collectionId,
        Collection collection,
        CurrentContextOrganization organization)
    {
        organizationUser.UserId = otherUserId;
        collection.Id = collectionId;
        collection.OrganizationId = organizationUser.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationUser.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [collectionId]);

        Assert.Contains(collectionId, result.UnauthorizedPostedCollectionIds);
        Assert.False(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_CurrentCollectionUnauthorized_PreservedNotRejected(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId,
        Guid collectionId,
        Collection collection,
        CurrentContextOrganization organization)
    {
        organizationUser.UserId = otherUserId;
        collection.Id = collectionId;
        collection.OrganizationId = organizationUser.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var currentAccess = new List<CollectionAccessSelection> { new() { Id = collectionId } };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, currentAccess));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationUser.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, []);

        Assert.Contains(collectionId, result.ReadonlyCurrentCollectionIds);
        Assert.Empty(result.UnauthorizedPostedCollectionIds);
        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_NonexistentPostedCollectionId_SilentlySkipped(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId,
        Guid collectionId)
    {
        organizationUser.UserId = otherUserId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [collectionId]);

        Assert.Empty(result.UnauthorizedPostedCollectionIds);
        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WhenCallerManagesCollection_Authorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId,
        Guid collectionId,
        Collection collection,
        CurrentContextOrganization organization)
    {
        organizationUser.UserId = otherUserId;
        collection.Id = collectionId;
        collection.OrganizationId = organizationUser.OrganizationId;
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationUser.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(userId)
            .Returns(new List<CollectionDetails> { new() { Id = collectionId, Manage = true } });

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [collectionId]);

        Assert.True(result.IsSuccess);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeUpdateAsync_WhenProviderUser_FullyAuthorized(
        SutProvider<OrganizationUserAuthorizationService> sutProvider,
        OrganizationUser organizationUser,
        Guid organizationUserId,
        Guid userId,
        Guid otherUserId,
        Guid collectionId,
        Collection collection)
    {
        organizationUser.UserId = otherUserId;
        collection.Id = collectionId;
        collection.OrganizationId = organizationUser.OrganizationId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUserId)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser, new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organizationUser.OrganizationId).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationUser.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection, CollectionAccessDetails>(collection, new CollectionAccessDetails { Users = [], Groups = [] }));

        var result = await sutProvider.Sut.AuthorizeUpdateAsync(organizationUser.OrganizationId, organizationUserId, [collectionId]);

        Assert.True(result.IsSuccess);
    }
}
