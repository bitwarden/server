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
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var orgUser = await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);

        // Act: update the user, including collection access so we test this overloaded method
        orgUser.Type = OrganizationUserType.Admin;
        orgUser.AccessSecretsManager = true;
        orgUser.RevisionDate = DateTime.UtcNow.AddMinutes(10);

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

        // Collection revision date should match the orgUser's RevisionDate
        var (actualCollection, _) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);
        Assert.NotNull(actualCollection);
        Assert.Equal(orgUser.RevisionDate, actualCollection.RevisionDate, TimeSpan.FromMilliseconds(10));
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
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var user = await userRepository.CreateTestUserAsync();
        // OrganizationUser is linked with the User in the Confirmed status
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        // Act: update the user, including collection access so we test this overloaded method
        orgUser.Type = OrganizationUserType.Admin;
        orgUser.AccessSecretsManager = true;
        orgUser.RevisionDate = DateTime.UtcNow.AddMinutes(10);

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

        // Collection revision date should match the orgUser's RevisionDate
        var (actualCollection, _) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);
        Assert.NotNull(actualCollection);
        Assert.Equal(orgUser.RevisionDate, actualCollection.RevisionDate, TimeSpan.FromMilliseconds(10));
    }

    /// <summary>
    /// The persistence path behind the single-member PAM grant and revoke.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_RoundTripsAccessPam(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        Assert.False(orgUser.AccessPam);

        orgUser.AccessPam = true;
        await organizationUserRepository.ReplaceAsync(orgUser);

        var granted = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(granted);
        Assert.True(granted.AccessPam);

        granted.AccessPam = false;
        await organizationUserRepository.ReplaceAsync(granted);

        var revoked = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(revoked);
        Assert.False(revoked.AccessPam);
    }

    /// <summary>
    /// The persistence path behind the bulk PAM grant, which writes several members in one statement.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceManyAsync_RoundTripsAccessPam(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var firstUser = await userRepository.CreateTestUserAsync("pam-first");
        var secondUser = await userRepository.CreateTestUserAsync("pam-second");
        var firstOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, firstUser);
        var secondOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, secondUser);

        // Load through the repository first, as the bulk endpoint does: the EF implementation can only update
        // entities that came from a query.
        var toUpdate = await organizationUserRepository.GetManyAsync([firstOrgUser.Id, secondOrgUser.Id]);
        foreach (var orgUser in toUpdate)
        {
            orgUser.AccessPam = true;
        }

        await organizationUserRepository.ReplaceManyAsync(toUpdate);

        var actual = await organizationUserRepository.GetManyAsync([firstOrgUser.Id, secondOrgUser.Id]);
        Assert.Equal(2, actual.Count);
        Assert.All(actual, ou => Assert.True(ou.AccessPam));
    }
}
