using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationSubscriptionUpdateRepositoryTests
{
    [DatabaseData, DatabaseTheory]
    public async Task SetToUpdateSubscriptionAsync_GivenOrganizationHasNotChangedSeatCountBefore_WhenUpdatingOrgSeats_ThenSubscriptionUpdateIsSaved(
        IOrganizationSubscriptionUpdateRepository sutRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requestDate = DateTime.UtcNow;

        // Act
        await sutRepository.SetToUpdateSubscriptionAsync(organization.Id, requestDate);

        // Assert
        var result = (await sutRepository.GetUpdatesToSubscriptionAsync()).ToArray();

        var updateResult = result.FirstOrDefault(x => x.OrganizationId == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.OrganizationId);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.SeatsLastUpdated?.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await organizationRepository.DeleteAsync(organization);
    }

    [DatabaseData, DatabaseTheory]
    public async Task SetToUpdateSubscriptionAsync_GivenOrganizationHasChangedSeatCountBeforeAndRecordExists_WhenUpdatingOrgSeats_ThenSubscriptionUpdateIsSaved(
        IOrganizationSubscriptionUpdateRepository sutRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await sutRepository.SetToUpdateSubscriptionAsync(organization.Id, DateTime.UtcNow); // previous update

        var requestDate = DateTime.UtcNow;

        // Act
        await sutRepository.SetToUpdateSubscriptionAsync(organization.Id, requestDate);

        // Assert
        var result = (await sutRepository.GetUpdatesToSubscriptionAsync()).ToArray();
        var updateResult = result.FirstOrDefault(x => x.OrganizationId == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.OrganizationId);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.SeatsLastUpdated?.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await organizationRepository.DeleteAsync(organization);
    }

    [DatabaseData, DatabaseTheory]
    public async Task GetUpdatesToSubscriptionAsync_GivenOrganizationHasChangedSeatCount_WhenGettingOrgsToUpdate_ThenReturnsOrgSubscriptionUpdate(
        IOrganizationSubscriptionUpdateRepository sutRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requestDate = DateTime.UtcNow;
        await sutRepository.SetToUpdateSubscriptionAsync(organization.Id, requestDate);

        // Act
        var result = (await sutRepository.GetUpdatesToSubscriptionAsync()).ToArray();

        // Assert
        var updateResult = result.FirstOrDefault(x => x.OrganizationId == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.OrganizationId);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.SeatsLastUpdated?.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await organizationRepository.DeleteAsync(organization);
    }

    [DatabaseData, DatabaseTheory]
    public async Task UpdateSubscriptionStatusAsync_GivenOrganizationHasChangedSeatCount_WhenUpdatingStatus_ThenSuccessfulRunNullsOutDateAndResetsCount(
        IOrganizationSubscriptionUpdateRepository sutRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requestDate = DateTime.UtcNow;
        await sutRepository.SetToUpdateSubscriptionAsync(organization.Id, requestDate);

        // Act
        await sutRepository.UpdateSubscriptionStatusAsync([organization.Id], []);

        // Assert
        var result = (await sutRepository.GetUpdatesToSubscriptionAsync()).ToArray();
        Assert.Empty(result);

        // Annul
        await organizationRepository.DeleteAsync(organization);
    }

    [DatabaseData, DatabaseTheory]
    public async Task UpdateSubscriptionStatusAsync_GivenOrganizationHasChangedSeatCount_WhenUpdatingStatus_ThenFailedRunUpdatesCount(
        IOrganizationSubscriptionUpdateRepository sutRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var requestDate = DateTime.UtcNow;
        await sutRepository.SetToUpdateSubscriptionAsync(organization.Id, requestDate);

        // Act
        await sutRepository.UpdateSubscriptionStatusAsync([], [organization.Id]);

        // Assert
        var result = (await sutRepository.GetUpdatesToSubscriptionAsync()).ToArray();
        var updateResult = result.FirstOrDefault(x => x.OrganizationId == organization.Id);
        Assert.NotNull(updateResult);
        Assert.Equal(organization.Id, updateResult.OrganizationId);
        Assert.Equal(1, updateResult.SyncAttempts);
        Assert.Equal(requestDate.ToString("yyyy-MM-dd HH:mm:ss"), updateResult.SeatsLastUpdated?.ToString("yyyy-MM-dd HH:mm:ss"));

        // Annul
        await organizationRepository.DeleteAsync(organization);
    }
}
