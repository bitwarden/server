using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// Covers how the collection reads combine a member's grants on one collection: the highest permission level wins
/// (Manage, then Edit, then ViewPassword), and a direct grant overrides every group grant.
/// </summary>
public class CollectionRepositoryHighestPermissionTests
{
    [Theory, DatabaseData]
    public async Task CollectionReads_GroupsGrantEditExceptPasswordsAndViewOnly_ReturnHighestPermissionLevel(
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
        var editExceptPasswordsGroup = await groupRepository.CreateTestGroupAsync(organization, "edit except passwords");
        var viewGroup = await groupRepository.CreateTestGroupAsync(organization, "view");
        await groupRepository.UpdateUsersAsync(editExceptPasswordsGroup.Id, [orgUser.Id], DateTime.UtcNow);
        await groupRepository.UpdateUsersAsync(viewGroup.Id, [orgUser.Id], DateTime.UtcNow);
        var collection = new Collection { Name = $"test {Guid.NewGuid()}", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                new CollectionAccessSelection
                {
                    Id = editExceptPasswordsGroup.Id, ReadOnly = false, HidePasswords = true, Manage = false
                },
                new CollectionAccessSelection
                {
                    Id = viewGroup.Id, ReadOnly = true, HidePasswords = false, Manage = false
                }
            ],
            users: []);

        // Act & Assert
        await AssertAllReadsAsync(collectionRepository, user, collection,
            readOnly: false, hidePasswords: true, manage: false);
    }

    [Theory, DatabaseData]
    public async Task CollectionReads_DirectGrantAndGroupGrantOnSameCollection_UseDirectGrant(
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
        var manageGroup = await groupRepository.CreateTestGroupAsync(organization, "manage");
        await groupRepository.UpdateUsersAsync(manageGroup.Id, [orgUser.Id], DateTime.UtcNow);
        var collection = new Collection { Name = $"test {Guid.NewGuid()}", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection,
            groups:
            [
                new CollectionAccessSelection
                {
                    Id = manageGroup.Id, ReadOnly = false, HidePasswords = false, Manage = true
                }
            ],
            users:
            [
                new CollectionAccessSelection
                {
                    Id = orgUser.Id, ReadOnly = true, HidePasswords = false, Manage = false
                }
            ]);

        // Act & Assert
        await AssertAllReadsAsync(collectionRepository, user, collection,
            readOnly: true, hidePasswords: false, manage: false);
    }

    private static async Task AssertAllReadsAsync(
        ICollectionRepository collectionRepository,
        User user,
        Collection collection,
        bool readOnly,
        bool hidePasswords,
        bool manage)
    {
        var byUserId = Assert.Single(await collectionRepository.GetManyByUserIdAsync(user.Id), c => c.Id == collection.Id);
        Assert.Equal((readOnly, hidePasswords, manage), (byUserId.ReadOnly, byUserId.HidePasswords, byUserId.Manage));

        var byId = await collectionRepository.GetByIdWithPermissionsAsync(collection.Id, user.Id, false);
        Assert.NotNull(byId);
        AssertAdminDetails(byId, readOnly, hidePasswords, manage);

        var shared = await collectionRepository.GetManySharedByOrganizationIdWithPermissionsAsync(
            collection.OrganizationId, user.Id, false);
        AssertAdminDetails(Assert.Single(shared, c => c.Id == collection.Id), readOnly, hidePasswords, manage);

        var all = await collectionRepository.GetManyOrganizationCollectionsWithPermissionsAsync(
            collection.OrganizationId, user.Id);
        AssertAdminDetails(Assert.Single(all, c => c.Id == collection.Id), readOnly, hidePasswords, manage);
    }

    private static void AssertAdminDetails(CollectionAdminDetails actual, bool readOnly, bool hidePasswords, bool manage)
    {
        Assert.True(actual.Assigned);
        Assert.Equal((readOnly, hidePasswords, manage), (actual.ReadOnly, actual.HidePasswords, actual.Manage));
    }
}
