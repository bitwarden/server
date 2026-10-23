using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Seeder.Extensions;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Extensions;

public class SeederRepositoryGuardExtensionsTests
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _otherOwnerId = Guid.NewGuid();

    public static TheoryData<Guid[]?> NullOrEmptyIds() => new() { null, Array.Empty<Guid>() };

    [Theory]
    [MemberData(nameof(NullOrEmptyIds))]
    public async Task ThrowIfProjectsNotInOrganizationAsync_NullOrEmpty_DoesNotQuery(Guid[]? ids)
    {
        var repository = Substitute.For<IProjectRepository>();

        await repository.ThrowIfProjectsNotInOrganizationAsync(ids, _ownerId);

        await repository.DidNotReceiveWithAnyArgs().ProjectsAreInOrganization(default!, default);
    }

    [Fact]
    public async Task ThrowIfProjectsNotInOrganizationAsync_DuplicateIds_QueriesDistinctIds()
    {
        var id = Guid.NewGuid();
        var repository = Substitute.For<IProjectRepository>();
        repository.ProjectsAreInOrganization(Arg.Any<List<Guid>>(), _ownerId).Returns(true);

        await repository.ThrowIfProjectsNotInOrganizationAsync([id, id], _ownerId);

        await repository.Received(1).ProjectsAreInOrganization(
            Arg.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { id })), _ownerId);
    }

    [Fact]
    public async Task ThrowIfServiceAccountsNotInOrganizationAsync_DuplicateIds_QueriesDistinctIds()
    {
        var id = Guid.NewGuid();
        var repository = Substitute.For<IServiceAccountRepository>();
        repository.ServiceAccountsAreInOrganizationAsync(Arg.Any<List<Guid>>(), _ownerId).Returns(true);

        await repository.ThrowIfServiceAccountsNotInOrganizationAsync([id, id], _ownerId);

        await repository.Received(1).ServiceAccountsAreInOrganizationAsync(
            Arg.Is<List<Guid>>(ids => ids.SequenceEqual(new[] { id })), _ownerId);
    }

    [Fact]
    public async Task ThrowIfGroupsNotInOrganizationAsync_InOtherOrganization_Throws()
    {
        var group = NewGroup(_otherOwnerId);
        var repository = Substitute.For<IGroupRepository>();
        repository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([group]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfGroupsNotInOrganizationAsync([group.Id], _ownerId));
    }

    [Fact]
    public async Task ThrowIfOrganizationUsersNotInOrganizationAsync_InOtherOrganization_Throws()
    {
        var organizationUser = NewOrganizationUser(_otherOwnerId, OrganizationUserStatusType.Confirmed);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([organizationUser]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfOrganizationUsersNotInOrganizationAsync([organizationUser.Id], _ownerId));
    }

    [Fact]
    public async Task ThrowIfCollectionsNotInOrganizationAsync_AllInOrganization_Passes()
    {
        var collection = NewCollection(_ownerId);
        var repository = Substitute.For<ICollectionRepository>();
        repository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        await repository.ThrowIfCollectionsNotInOrganizationAsync([collection.Id], _ownerId);
    }

    [Fact]
    public async Task ThrowIfCollectionsNotInOrganizationAsync_InOtherOrganization_Throws()
    {
        var collection = NewCollection(_otherOwnerId);
        var repository = Substitute.For<ICollectionRepository>();
        repository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfCollectionsNotInOrganizationAsync([collection.Id], _ownerId));
    }

    [Fact]
    public async Task ThrowIfCollectionsNotInOrganizationAsync_Missing_Throws()
    {
        var collection = NewCollection(_ownerId);
        var repository = Substitute.For<ICollectionRepository>();
        repository.GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([collection]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfCollectionsNotInOrganizationAsync([collection.Id, Guid.NewGuid()], _ownerId));
    }

    [Fact]
    public async Task GetDistinctCollectionIdsOrThrowAsync_OrganizationMissing_ThrowsWithoutQueryingCollections()
    {
        var organizationRepository = Substitute.For<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(_ownerId).Returns((Organization?)null);
        var collectionRepository = Substitute.For<ICollectionRepository>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            organizationRepository.GetDistinctCollectionIdsOrThrowAsync(collectionRepository, _ownerId, [Guid.NewGuid()]));

        await collectionRepository.DidNotReceiveWithAnyArgs().GetManyByManyIdsAsync(default!);
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public async Task ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync_ConfirmedOrRevokedMember_Passes(
        OrganizationUserStatusType status)
    {
        var membership = NewOrganizationUser(_ownerId, status);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([membership]);

        await repository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(membership.UserId!.Value, _ownerId);
    }

    [Fact]
    public async Task ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync_MemberOfOtherOrganization_Throws()
    {
        var membership = NewOrganizationUser(_otherOwnerId, OrganizationUserStatusType.Confirmed);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([membership]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(membership.UserId!.Value, _ownerId));
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusType.Accepted)]
    public async Task ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync_UnconfirmedMember_Throws(
        OrganizationUserStatusType status)
    {
        var membership = NewOrganizationUser(_ownerId, status);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([membership]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfUserNotConfirmedOrRevokedMemberOfOrganizationAsync(membership.UserId!.Value, _ownerId));
    }

    [Fact]
    public async Task ThrowIfFoldersNotOwnedByUserAsync_OwnedByUser_Passes()
    {
        var folder = NewFolder(_ownerId);
        var repository = Substitute.For<IFolderRepository>();
        repository.GetManyByUserIdAsync(_ownerId).Returns([folder]);

        await repository.ThrowIfFoldersNotOwnedByUserAsync([folder.Id], _ownerId);
    }

    [Fact]
    public async Task ThrowIfFoldersNotOwnedByUserAsync_Missing_Throws()
    {
        var repository = Substitute.For<IFolderRepository>();
        repository.GetManyByUserIdAsync(_ownerId).Returns([NewFolder(_ownerId)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfFoldersNotOwnedByUserAsync([Guid.NewGuid()], _ownerId));
    }

    [Fact]
    public async Task ThrowIfFolderNotOwnedByUserAsync_Null_DoesNotQuery()
    {
        var repository = Substitute.For<IFolderRepository>();

        await repository.ThrowIfFolderNotOwnedByUserAsync(null, _ownerId);

        await repository.DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
    }

    [Theory]
    [MemberData(nameof(NullOrEmptyIds))]
    public async Task ThrowIfOrganizationsLinkedToProviderAsync_NullOrEmpty_DoesNotQuery(Guid[]? ids)
    {
        var repository = Substitute.For<IProviderOrganizationRepository>();

        await repository.ThrowIfOrganizationsLinkedToProviderAsync(ids);

        await repository.DidNotReceiveWithAnyArgs().GetCountByOrganizationIdsAsync(default!);
        await repository.DidNotReceiveWithAnyArgs().GetByOrganizationId(default);
    }

    [Fact]
    public async Task ThrowIfOrganizationsLinkedToProviderAsync_NotLinked_Passes()
    {
        var repository = Substitute.For<IProviderOrganizationRepository>();
        repository.GetCountByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(0);

        await repository.ThrowIfOrganizationsLinkedToProviderAsync([Guid.NewGuid(), Guid.NewGuid()]);

        await repository.Received(1).GetCountByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>());
        await repository.DidNotReceiveWithAnyArgs().GetByOrganizationId(default);
    }

    [Fact]
    public async Task ThrowIfOrganizationsLinkedToProviderAsync_Linked_ThrowsWithOrganizationId()
    {
        var unlinkedId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        var repository = Substitute.For<IProviderOrganizationRepository>();
        repository.GetCountByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(1);
        repository.GetByOrganizationId(linkedId)
            .Returns(new ProviderOrganization { ProviderId = Guid.NewGuid(), OrganizationId = linkedId });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfOrganizationsLinkedToProviderAsync([unlinkedId, linkedId, linkedId]));

        Assert.Contains("already belongs to a provider", exception.Message);
        Assert.Contains(linkedId.ToString(), exception.Message);
        await repository.Received(1).GetCountByOrganizationIdsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { unlinkedId, linkedId })));
        await repository.Received(1).GetByOrganizationId(linkedId);
    }

    private static Group NewGroup(Guid organizationId) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Name = "group" };

    private static Collection NewCollection(Guid organizationId) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Name = "collection" };

    private static Folder NewFolder(Guid userId) =>
        new() { Id = Guid.NewGuid(), UserId = userId, Name = "folder" };

    private static OrganizationUser NewOrganizationUser(Guid organizationId, OrganizationUserStatusType status) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = Guid.NewGuid(), Status = status };
}
