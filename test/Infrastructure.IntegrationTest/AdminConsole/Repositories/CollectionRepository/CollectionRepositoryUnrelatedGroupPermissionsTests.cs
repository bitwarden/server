using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// Covers how the admin collection reads combine a member's group grants when the member also belongs to groups
/// that have no grant on the collection.
///
/// These reads join every group the member belongs to, so an unrelated group still produces a row with no grant.
/// That row must be ignored when combining permissions; it must not be read as "not read-only" / "passwords
/// visible", which would override the restriction from the group that actually grants access.
/// </summary>
public class CollectionRepositoryUnrelatedGroupPermissionsTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdWithPermissionsAsync_ReadOnlyGroupPlusUnrelatedGroup_ReturnsReadOnly(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var (user, collection) = await ArrangeReadOnlyGroupPlusUnrelatedGroupAsync(
            userRepository, organizationRepository, organizationUserRepository, groupRepository, collectionRepository);

        // Act
        var actual = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, false);

        // Assert
        Assert.NotNull(actual);
        AssertReadOnlyHidePasswords(actual);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManySharedByOrganizationIdWithPermissionsAsync_ReadOnlyGroupPlusUnrelatedGroup_ReturnsReadOnly(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var (user, collection) = await ArrangeReadOnlyGroupPlusUnrelatedGroupAsync(
            userRepository, organizationRepository, organizationUserRepository, groupRepository, collectionRepository);

        // Act
        var actual = await collectionRepository.GetManySharedByOrganizationIdWithPermissionsAsync(
            collection.OrganizationId, user.Id, false);

        // Assert
        AssertReadOnlyHidePasswords(Assert.Single(actual, c => c.Id == collection.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyOrganizationCollectionsWithPermissionsAsync_ReadOnlyGroupPlusUnrelatedGroup_ReturnsReadOnly(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var (user, collection) = await ArrangeReadOnlyGroupPlusUnrelatedGroupAsync(
            userRepository, organizationRepository, organizationUserRepository, groupRepository, collectionRepository);

        // Act
        var actual = await collectionRepository.GetManyOrganizationCollectionsWithPermissionsAsync(
            collection.OrganizationId, user.Id);

        // Assert
        AssertReadOnlyHidePasswords(Assert.Single(actual, c => c.Id == collection.Id));
    }

    /// <summary>
    /// A member whose only groups have no grant on the collection is unassigned, and keeps reporting the default
    /// (not read-only, passwords visible) flags alongside <see cref="CollectionAdminDetails.Assigned"/> = false.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task WithPermissionsReads_OnlyUnrelatedGroups_ReturnsUnassignedWithDefaultFlags(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);
        var unrelatedGroup1 = await groupRepository.CreateTestGroupAsync(organization);
        var unrelatedGroup2 = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.UpdateUsersAsync(unrelatedGroup1.Id, [orgUser.Id], DateTime.UtcNow);
        await groupRepository.UpdateUsersAsync(unrelatedGroup2.Id, [orgUser.Id], DateTime.UtcNow);
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        // Act
        var byId = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, false);
        var shared = await collectionRepository.GetManySharedByOrganizationIdWithPermissionsAsync(
            organization.Id, user.Id, false);
        var all = await collectionRepository.GetManyOrganizationCollectionsWithPermissionsAsync(
            organization.Id, user.Id);

        // Assert
        Assert.NotNull(byId);
        AssertUnassigned(byId);
        AssertUnassigned(Assert.Single(shared, c => c.Id == collection.Id));
        AssertUnassigned(Assert.Single(all, c => c.Id == collection.Id));
    }

    private static async Task<(User User, Collection Collection)> ArrangeReadOnlyGroupPlusUnrelatedGroupAsync(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);

        var grantingGroup = await groupRepository.CreateTestGroupAsync(organization, "granting");
        var unrelatedGroup = await groupRepository.CreateTestGroupAsync(organization, "unrelated");
        await groupRepository.UpdateUsersAsync(grantingGroup.Id, [orgUser.Id], DateTime.UtcNow);
        await groupRepository.UpdateUsersAsync(unrelatedGroup.Id, [orgUser.Id], DateTime.UtcNow);

        var collection = new Collection { Name = $"test {Guid.NewGuid()}", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                new CollectionAccessSelection
                {
                    Id = grantingGroup.Id, ReadOnly = true, HidePasswords = true, Manage = false
                }
            ],
            users: []);

        return (user, collection);
    }

    private static void AssertReadOnlyHidePasswords(CollectionAdminDetails actual)
    {
        Assert.True(actual.Assigned);
        Assert.False(actual.Manage);
        Assert.True(actual.ReadOnly);
        Assert.True(actual.HidePasswords);
    }

    private static void AssertUnassigned(CollectionAdminDetails actual)
    {
        Assert.False(actual.Assigned);
        Assert.False(actual.Manage);
        Assert.False(actual.ReadOnly);
        Assert.False(actual.HidePasswords);
    }
}
