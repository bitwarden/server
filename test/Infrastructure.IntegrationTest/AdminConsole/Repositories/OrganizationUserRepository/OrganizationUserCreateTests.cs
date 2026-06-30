using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class OrganizationUserCreateTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_WithCollections_CreatesAccessAndBumpsCollectionRevisionDate(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var orgUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            RevisionDate = DateTime.UtcNow.AddMinutes(10),
        };

        await organizationUserRepository.CreateAsync(orgUser, [
            new CollectionAccessSelection { Id = collection.Id, Manage = true, HidePasswords = false, ReadOnly = false }
        ]);

        await AssertOrgUserAndCollectionRevisionDate(
            organizationUserRepository, collectionRepository,
            orgUser, collection.Id, orgUser.RevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateManyAsync_WithCollections_CreatesAccessAndBumpsCollectionRevisionDate(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        var orgUser = new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = null,
            Email = $"invite-{Guid.NewGuid()}@example.com",
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow.AddMinutes(10),
        };

        await organizationUserRepository.CreateManyAsync([
            new CreateOrganizationUser
            {
                OrganizationUser = orgUser,
                Collections = [new CollectionAccessSelection { Id = collection.Id, Manage = true }],
                Groups = [],
            }
        ]);

        await AssertOrgUserAndCollectionRevisionDate(
            organizationUserRepository, collectionRepository,
            orgUser, collection.Id, orgUser.RevisionDate);
    }

    private static async Task AssertOrgUserAndCollectionRevisionDate(
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        OrganizationUser expectedOrgUser,
        Guid collectionId,
        DateTime expectedCollectionRevisionDate)
    {
        var (actualOrgUser, actualCollections) = await organizationUserRepository.GetByIdWithCollectionsAsync(expectedOrgUser.Id);
        Assert.NotNull(actualOrgUser);
        Assert.Equal(expectedOrgUser.OrganizationId, actualOrgUser.OrganizationId);
        Assert.Equal(expectedOrgUser.Email, actualOrgUser.Email);
        Assert.Equal(expectedOrgUser.Status, actualOrgUser.Status);
        Assert.Equal(expectedOrgUser.Type, actualOrgUser.Type);

        var collectionAccess = Assert.Single(actualCollections);
        Assert.Equal(collectionId, collectionAccess.Id);
        Assert.True(collectionAccess.Manage);

        var (actualCollection, _) = await collectionRepository.GetByIdWithAccessAsync(collectionId);
        Assert.NotNull(actualCollection);
        Assert.Equal(expectedCollectionRevisionDate, actualCollection.RevisionDate, TimeSpan.FromMilliseconds(10));
    }
}
