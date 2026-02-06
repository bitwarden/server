using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class OrganizationUserReplaceTests
{
    /// <summary>
    /// Specifically tests OrganizationUsers in the invited state, which is unique because
    /// they're not linked to a UserId.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithCollectionAccess_WhenUserIsInvited_Success(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var orgUser = await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);

        // Act: update the user, including collection access so we test this overloaded method
        orgUser.Type = OrganizationUserType.Admin;
        orgUser.AccessSecretsManager = true;
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        await organizationUserRepository.ReplaceAsync(orgUser, [
            new CollectionAccessSelection { Id = collection.Id, Manage = true }
        ]);

        // Assert
        var (actualOrgUser, actualCollections) = await organizationUserRepository.GetByIdWithCollectionsAsync(orgUser.Id);
        Assert.NotNull(actualOrgUser);
        Assert.Equal(OrganizationUserType.Admin, actualOrgUser.Type);
        Assert.True(actualOrgUser.AccessSecretsManager);

        var collectionAccess = Assert.Single(actualCollections);
        Assert.Equal(collection.Id, collectionAccess.Id);
        Assert.True(collectionAccess.Manage);
    }

    /// <summary>
    /// Tests OrganizationUsers in the Confirmed status, which is a stand-in for all other
    /// non-Invited statuses (which are all linked to a UserId).
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithCollectionAccess_WhenUserIsConfirmed_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var user = await userRepository.CreateTestUserAsync();
        // OrganizationUser is linked with the User in the Confirmed status
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        // Act: update the user, including collection access so we test this overloaded method
        orgUser.Type = OrganizationUserType.Admin;
        orgUser.AccessSecretsManager = true;
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        await organizationUserRepository.ReplaceAsync(orgUser, [
            new CollectionAccessSelection { Id = collection.Id, Manage = true }
        ]);

        // Assert
        var (actualOrgUser, actualCollections) = await organizationUserRepository.GetByIdWithCollectionsAsync(orgUser.Id);
        Assert.NotNull(actualOrgUser);
        Assert.Equal(OrganizationUserType.Admin, actualOrgUser.Type);
        Assert.True(actualOrgUser.AccessSecretsManager);

        var collectionAccess = Assert.Single(actualCollections);
        Assert.Equal(collection.Id, collectionAccess.Id);
        Assert.True(collectionAccess.Manage);

        // Account revision date should be updated to a later date
        var actualUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(actualUser);
        Assert.True(actualUser.AccountRevisionDate.CompareTo(user.AccountRevisionDate) > 0);
    }
}
