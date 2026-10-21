using Bit.Core.AdminConsole.Entities;
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
    public async Task ThrowIfGroupsNotInOrganizationAsync_AllInOrganization_Passes()
    {
        var group = NewGroup(_ownerId);
        var repository = Substitute.For<IGroupRepository>();
        repository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([group]);

        await repository.ThrowIfGroupsNotInOrganizationAsync([group.Id], _ownerId);
    }

    [Fact]
    public async Task ThrowIfGroupsNotInOrganizationAsync_InOtherOrganization_Throws()
    {
        var group = NewGroup(_otherOwnerId);
        var repository = Substitute.For<IGroupRepository>();
        repository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([group]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfGroupsNotInOrganizationAsync([group.Id], _ownerId));

        Assert.Contains(_ownerId.ToString(), exception.Message);
    }

    [Fact]
    public async Task ThrowIfGroupsNotInOrganizationAsync_Missing_Throws()
    {
        var repository = Substitute.For<IGroupRepository>();
        repository.GetManyByManyIds(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfGroupsNotInOrganizationAsync([Guid.NewGuid()], _ownerId));
    }

    [Fact]
    public async Task ThrowIfOrganizationUsersNotInOrganizationAsync_AllInOrganization_Passes()
    {
        var organizationUser = NewOrganizationUser(_ownerId, OrganizationUserStatusType.Invited);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([organizationUser]);

        await repository.ThrowIfOrganizationUsersNotInOrganizationAsync([organizationUser.Id], _ownerId);
    }

    [Fact]
    public async Task ThrowIfOrganizationUsersNotInOrganizationAsync_InOtherOrganization_Throws()
    {
        var organizationUser = NewOrganizationUser(_otherOwnerId, OrganizationUserStatusType.Confirmed);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([organizationUser]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfOrganizationUsersNotInOrganizationAsync([organizationUser.Id], _ownerId));

        Assert.Contains(_ownerId.ToString(), exception.Message);
    }

    [Fact]
    public async Task ThrowIfOrganizationUsersNotInOrganizationAsync_Missing_Throws()
    {
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfOrganizationUsersNotInOrganizationAsync([Guid.NewGuid()], _ownerId));
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfCollectionsNotInOrganizationAsync([collection.Id], _ownerId));

        Assert.Contains(_ownerId.ToString(), exception.Message);
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
    public async Task ThrowIfUsersNotConfirmedMembersOfOrganizationAsync_ConfirmedMember_Passes()
    {
        var membership = NewOrganizationUser(_ownerId, OrganizationUserStatusType.Confirmed);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([membership]);

        await repository.ThrowIfUsersNotConfirmedMembersOfOrganizationAsync([membership.UserId!.Value], _ownerId);
    }

    [Fact]
    public async Task ThrowIfUsersNotConfirmedMembersOfOrganizationAsync_MemberOfOtherOrganization_Throws()
    {
        var membership = NewOrganizationUser(_otherOwnerId, OrganizationUserStatusType.Confirmed);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([membership]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfUsersNotConfirmedMembersOfOrganizationAsync([membership.UserId!.Value], _ownerId));

        Assert.Contains(_ownerId.ToString(), exception.Message);
    }

    [Fact]
    public async Task ThrowIfUsersNotConfirmedMembersOfOrganizationAsync_NotAMember_Throws()
    {
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfUsersNotConfirmedMembersOfOrganizationAsync([Guid.NewGuid()], _ownerId));
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public async Task ThrowIfUsersNotConfirmedMembersOfOrganizationAsync_UnconfirmedMember_Throws(
        OrganizationUserStatusType status)
    {
        var membership = NewOrganizationUser(_ownerId, status);
        var repository = Substitute.For<IOrganizationUserRepository>();
        repository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>()).Returns([membership]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfUsersNotConfirmedMembersOfOrganizationAsync([membership.UserId!.Value], _ownerId));
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfFoldersNotOwnedByUserAsync([Guid.NewGuid()], _ownerId));

        Assert.Contains(_ownerId.ToString(), exception.Message);
    }

    [Fact]
    public async Task ThrowIfOrganizationsLinkedToProviderAsync_NotLinked_Passes()
    {
        var repository = Substitute.For<IProviderOrganizationRepository>();
        repository.GetCountByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(0);

        await repository.ThrowIfOrganizationsLinkedToProviderAsync([Guid.NewGuid()]);
    }

    [Fact]
    public async Task ThrowIfOrganizationsLinkedToProviderAsync_Linked_Throws()
    {
        var repository = Substitute.For<IProviderOrganizationRepository>();
        repository.GetCountByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns(1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ThrowIfOrganizationsLinkedToProviderAsync([Guid.NewGuid()]));

        Assert.Contains("already belongs to a provider", exception.Message);
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
