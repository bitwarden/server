using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class GroupRepositoryTests
{
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

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds);

        // Assert
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(orgUserIds!.Order(), actual.Order());
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
        await groupRepository.UpdateUsersAsync(group.Id, [orgUser2.Id]);
        var existingUsers = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal([orgUser2.Id], existingUsers);

        // Act
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds);

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
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds);

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
        await groupRepository.AddGroupUsersByIdAsync(group.Id, orgUserIds);

        // Assert
        var actual = await groupRepository.GetManyUserIdsByIdAsync(group.Id);
        Assert.Equal(2, actual.Count);
        Assert.Contains(orgUser1.Id, actual);
        Assert.Contains(orgUser2.Id, actual);
    }
}
