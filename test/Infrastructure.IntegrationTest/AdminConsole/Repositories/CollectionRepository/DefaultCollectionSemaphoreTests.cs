using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// Tests for DefaultCollectionSemaphore table behavior including cascade deletions
/// </summary>
public class DefaultCollectionSemaphoreTests
{
    [Theory, DatabaseData]
    public async Task DeleteOrganizationUser_CascadeDeletesSemaphore(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            [orgUser.Id],
            "My Items");

        // Verify semaphore exists
        var semaphoreBefore = await collectionRepository.GetDefaultCollectionSemaphoresAsync(organization.Id);
        Assert.Single(semaphoreBefore, s => s == orgUser.Id);

        // Act - Delete organization user
        await organizationUserRepository.DeleteAsync(orgUser);

        // Assert - Semaphore should be cascade deleted
        var semaphoreAfter = await collectionRepository.GetDefaultCollectionSemaphoresAsync(organization.Id);
        Assert.Empty(semaphoreAfter);
    }

    /// <summary>
    /// Test that deleting an Organization cascades through OrganizationUser to DefaultCollectionSemaphore
    /// Note: Cascade path is Organization -> OrganizationUser -> DefaultCollectionSemaphore (not direct)
    /// </summary>
    [Theory, DatabaseData]
    public async Task DeleteOrganization_CascadeDeletesSemaphore_ThroughOrganizationUser(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            [orgUser.Id],
            "My Items");

        // Verify semaphore exists
        var semaphoreBefore = await collectionRepository.GetDefaultCollectionSemaphoresAsync(organization.Id);
        Assert.Single(semaphoreBefore, s => s == orgUser.Id);

        // Act - Delete organization (which cascades to OrganizationUser, which cascades to semaphore)
        await organizationRepository.DeleteAsync(organization);

        // Assert - Semaphore should be cascade deleted via OrganizationUser
        var semaphoreAfter = await collectionRepository.GetDefaultCollectionSemaphoresAsync(organization.Id);
        Assert.Empty(semaphoreAfter);
    }
}
