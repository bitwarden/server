using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class GroupRepositoryTests
{
    [Theory, DatabaseData]
    public async Task GetManyGroupUsersByGroupIdsAsync_ReturnsOnlyRequestedMembershipsInOrganization(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var member = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var otherMember = await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, user);
        var group = await groupRepository.CreateTestGroupAsync(organization);
        var secondGroup = await groupRepository.CreateTestGroupAsync(organization);
        var emptyGroup = await groupRepository.CreateTestGroupAsync(organization);
        var unrequestedGroup = await groupRepository.CreateTestGroupAsync(organization);
        var otherGroup = await groupRepository.CreateTestGroupAsync(otherOrganization);
        foreach (var id in new[] { group.Id, secondGroup.Id, unrequestedGroup.Id })
        {
            await groupRepository.UpdateUsersAsync(id, [member.Id], DateTime.UtcNow);
        }
        await groupRepository.UpdateUsersAsync(otherGroup.Id, [otherMember.Id], DateTime.UtcNow);

        var memberships = await groupRepository.GetManyGroupUsersByGroupIdsAsync(
            organization.Id, [group.Id, secondGroup.Id, emptyGroup.Id, otherGroup.Id, Guid.NewGuid()]);

        Assert.Equal(
            new[] { (group.Id, member.Id), (secondGroup.Id, member.Id) }.OrderBy(pair => pair.Item1),
            memberships.Select(membership => (membership.GroupId, membership.OrganizationUserId)).OrderBy(pair => pair.GroupId));
        Assert.Empty(await groupRepository.GetManyGroupUsersByGroupIdsAsync(organization.Id, []));
    }

    [Theory, DatabaseData]
    public async Task GetManyGroupUsersByOrganizationUserIdsAsync_ReturnsOnlyRequestedMembershipsInOrganization(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var secondUser = await userRepository.CreateTestUserAsync();
        var emptyUser = await userRepository.CreateTestUserAsync();
        var unrequestedUser = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var member = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var secondMember = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, secondUser);
        var emptyMember = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, emptyUser);
        var unrequestedMember = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, unrequestedUser);
        var otherMember = await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, user);
        var group = await groupRepository.CreateTestGroupAsync(organization);
        var otherGroup = await groupRepository.CreateTestGroupAsync(otherOrganization);
        await groupRepository.UpdateUsersAsync(group.Id, [member.Id, secondMember.Id, unrequestedMember.Id], DateTime.UtcNow);
        await groupRepository.UpdateUsersAsync(otherGroup.Id, [otherMember.Id], DateTime.UtcNow);

        var memberships = await groupRepository.GetManyGroupUsersByOrganizationUserIdsAsync(
            organization.Id, [member.Id, secondMember.Id, emptyMember.Id, otherMember.Id, Guid.NewGuid()]);

        Assert.Equal(
            new[] { (group.Id, member.Id), (group.Id, secondMember.Id) }.OrderBy(pair => pair.Item2),
            memberships.Select(membership => (membership.GroupId, membership.OrganizationUserId)).OrderBy(pair => pair.OrganizationUserId));
        Assert.Empty(await groupRepository.GetManyGroupUsersByOrganizationUserIdsAsync(organization.Id, []));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_WithCollections_CreatesGroupAccessAndBumpsCollectionRevisionDate(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var collection1 = await collectionRepository.CreateTestCollectionAsync(org);
        var collection2 = await collectionRepository.CreateTestCollectionAsync(org);

        var group = new Group { OrganizationId = org.Id, Name = "New Group", RevisionDate = DateTime.UtcNow.AddMinutes(10) };
        await groupRepository.CreateAsync(group, [
            new CollectionAccessSelection { Id = collection1.Id, Manage = true, HidePasswords = false, ReadOnly = false },
            new CollectionAccessSelection { Id = collection2.Id, Manage = false, HidePasswords = true, ReadOnly = true },
        ]);

        var (actualGroup, actualCollections) = await groupRepository.GetByIdWithCollectionsAsync(group.Id);
        Assert.NotNull(actualGroup);
        Assert.Equal("New Group", actualGroup.Name);
        Assert.Equal(2, actualCollections.Count);
        Assert.Single(actualCollections, c => c.Id == collection1.Id && c.Manage && !c.HidePasswords && !c.ReadOnly);
        Assert.Single(actualCollections, c => c.Id == collection2.Id && !c.Manage && c.HidePasswords && c.ReadOnly);

        var (actualCollection1, _) = await collectionRepository.GetByIdWithAccessAsync(collection1.Id);
        var (actualCollection2, _) = await collectionRepository.GetByIdWithAccessAsync(collection2.Id);
        Assert.NotNull(actualCollection1);
        Assert.NotNull(actualCollection2);
        Assert.Equal(group.RevisionDate, actualCollection1.RevisionDate, TimeSpan.FromMilliseconds(10));
        Assert.Equal(group.RevisionDate, actualCollection2.RevisionDate, TimeSpan.FromMilliseconds(10));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithCollections_UpdatesGroupAndBumpsCollectionRevisionDate(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var group = await groupRepository.CreateTestGroupAsync(org);
        var collection1 = await collectionRepository.CreateTestCollectionAsync(org);
        var collection2 = await collectionRepository.CreateTestCollectionAsync(org);

        // Act
        group.Name = "Updated Group Name";
        group.RevisionDate = DateTime.UtcNow.AddMinutes(10);
        await groupRepository.ReplaceAsync(group, [
            new CollectionAccessSelection { Id = collection1.Id, Manage = true, HidePasswords = false, ReadOnly = false },
            new CollectionAccessSelection { Id = collection2.Id, Manage = false, HidePasswords = true, ReadOnly = true },
        ]);

        // Assert
        var (actualGroup, actualCollections) = await groupRepository.GetByIdWithCollectionsAsync(group.Id);
        Assert.NotNull(actualGroup);
        Assert.Equal("Updated Group Name", actualGroup.Name);

        Assert.Equal(2, actualCollections.Count);
        Assert.Single(actualCollections, c => c.Id == collection1.Id && c.Manage && !c.HidePasswords && !c.ReadOnly);
        Assert.Single(actualCollections, c => c.Id == collection2.Id && !c.Manage && c.HidePasswords && c.ReadOnly);

        var (actualCollection1, _) = await collectionRepository.GetByIdWithAccessAsync(collection1.Id);
        var (actualCollection2, _) = await collectionRepository.GetByIdWithAccessAsync(collection2.Id);
        Assert.NotNull(actualCollection1);
        Assert.NotNull(actualCollection2);
        Assert.Equal(group.RevisionDate, actualCollection1.RevisionDate, TimeSpan.FromMilliseconds(10));
        Assert.Equal(group.RevisionDate, actualCollection2.RevisionDate, TimeSpan.FromMilliseconds(10));
    }

    [DatabaseTheory, DatabaseData]
    public async Task AddGroupUsersByIdAsync_CreatesGroupUsers(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var user3 = await userRepository.CreateTestUserAsync("user3");

        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user2);
        var orgUser3 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user3);
        var orgUserIds = new List<Guid>([orgUser1.Id, orgUser2.Id, orgUser3.Id]);
        var group = await groupRepository.CreateTestGroupAsync(org);

        var expectedRevisionDate = DateTime.UtcNow.AddMinutes(10);

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds, expectedRevisionDate);

        // Assert
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(orgUserIds!.Order(), actual.Order());

        var actualGroup = await groupRepository.GetByIdAsync(group.Id);
        Assert.NotNull(actualGroup);
        Assert.Equal(expectedRevisionDate, actualGroup.RevisionDate, TimeSpan.FromMilliseconds(10));
    }

    [DatabaseTheory, DatabaseData]
    public async Task AddGroupUsersByIdAsync_IgnoresExistingGroupUsers(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var user3 = await userRepository.CreateTestUserAsync("user3");

        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user2);
        var orgUser3 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user3);
        var orgUserIds = new List<Guid>([orgUser1.Id, orgUser2.Id, orgUser3.Id]);
        var group = await groupRepository.CreateTestGroupAsync(org);

        // Add user 2 to the group already, make sure this is executed correctly before proceeding
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser2.Id], DateTime.UtcNow);
        var existingUsers = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal([orgUser2.Id], existingUsers);

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds, DateTime.UtcNow);

        // Assert - group should contain all users
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(orgUserIds!.Order(), actual.Order());
    }

    [DatabaseTheory, DatabaseData]
    public async Task AddGroupUsersByIdAsync_IgnoresUsersNotInOrganization(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var user3 = await userRepository.CreateTestUserAsync("user3");

        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user2);

        // User3 belongs to a different org
        var otherOrg = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser3 = await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrg, user3);

        var orgUserIds = new List<Guid>([orgUser1.Id, orgUser2.Id, orgUser3.Id]);
        var group = await groupRepository.CreateTestGroupAsync(org);

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds, DateTime.UtcNow);

        // Assert
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(2, actual.Count);
        Assert.Contains(orgUser1.Id, actual);
        Assert.Contains(orgUser2.Id, actual);
        Assert.DoesNotContain(orgUser3.Id, actual);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AddGroupUsersByIdAsync_IgnoresDuplicateUsers(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");

        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user2);

        var orgUserIds = new List<Guid>([orgUser1.Id, orgUser2.Id, orgUser2.Id]); // duplicate orgUser2
        var group = await groupRepository.CreateTestGroupAsync(org);

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds, DateTime.UtcNow);

        // Assert
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(2, actual.Count);
        Assert.Contains(orgUser1.Id, actual);
        Assert.Contains(orgUser2.Id, actual);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateUsersAsync_BumpsGroupRevisionDate(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");

        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user2);
        var group = await groupRepository.CreateTestGroupAsync(org);

        var expectedRevisionDate = DateTime.UtcNow.AddMinutes(10);

        // Act
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser1.Id, orgUser2.Id], expectedRevisionDate);

        // Assert
        var actualGroup = await groupRepository.GetByIdAsync(group.Id);
        Assert.NotNull(actualGroup);
        Assert.Equal(expectedRevisionDate, actualGroup.RevisionDate, TimeSpan.FromMilliseconds(10));
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteUserAsync_BumpsGroupRevisionDate(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        var group = await groupRepository.CreateTestGroupAsync(org);

        await groupRepository.UpdateUsersAsync(group.Id, [orgUser.Id], DateTime.UtcNow);
        var existingUsers = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal([orgUser.Id], existingUsers);

        var expectedRevisionDate = DateTime.UtcNow.AddMinutes(10);

        // Act
        await groupRepository.DeleteUserAsync(group.Id, orgUser.Id, expectedRevisionDate);

        // Assert
        var actualGroup = await groupRepository.GetByIdAsync(group.Id);
        Assert.NotNull(actualGroup);
        Assert.Equal(expectedRevisionDate, actualGroup.RevisionDate, TimeSpan.FromMilliseconds(10));
    }

}
