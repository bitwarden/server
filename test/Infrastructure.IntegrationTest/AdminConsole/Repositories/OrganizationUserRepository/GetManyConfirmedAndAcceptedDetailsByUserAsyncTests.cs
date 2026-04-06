using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetManyConfirmedAndAcceptedDetailsByUserAsyncTests
{
    [Theory, DatabaseData]
    public async Task ReturnsDetails_WhenUserIsConfirmed(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);

        // Act
        var results = await organizationUserRepository.GetManyConfirmedAndAcceptedDetailsByUserAsync(user.Id);

        // Assert
        Assert.Single(results);
        var result = results.Single();
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result.Status);

        // Annul
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteAsync(user);
    }

    [Theory, DatabaseData]
    public async Task ReturnsDetails_WhenUserIsAccepted(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(organization, user);

        // Act
        var results = await organizationUserRepository.GetManyConfirmedAndAcceptedDetailsByUserAsync(user.Id);

        // Assert
        Assert.Single(results);
        var result = results.Single();
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(OrganizationUserStatusType.Accepted, result.Status);

        // Annul
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteAsync(user);
    }

    [Theory, DatabaseData]
    public async Task ReturnsDetailsAcrossMultipleOrganizations_WhenUserIsConfirmedOrAccepted(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();

        var confirmedOrg = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(confirmedOrg, user);

        var acceptedOrg = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(acceptedOrg, user);

        // Act
        var results = await organizationUserRepository.GetManyConfirmedAndAcceptedDetailsByUserAsync(user.Id);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.OrganizationId == confirmedOrg.Id && r.Status == OrganizationUserStatusType.Confirmed);
        Assert.Contains(results, r => r.OrganizationId == acceptedOrg.Id && r.Status == OrganizationUserStatusType.Accepted);

        // Annul
        await organizationRepository.DeleteAsync(confirmedOrg);
        await organizationRepository.DeleteAsync(acceptedOrg);
        await userRepository.DeleteAsync(user);
    }

    [Theory, DatabaseData]
    public async Task DoesNotReturnDetails_WhenUserIsInvited(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);

        // Act
        var results = await organizationUserRepository.GetManyConfirmedAndAcceptedDetailsByUserAsync(user.Id);

        // Assert
        Assert.DoesNotContain(results, r => r.OrganizationId == organization.Id);

        // Annul
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteAsync(user);
    }

    [Theory, DatabaseData]
    public async Task DoesNotReturnDetails_WhenUserIsRevoked(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(organization, user);

        // Act
        var results = await organizationUserRepository.GetManyConfirmedAndAcceptedDetailsByUserAsync(user.Id);

        // Assert
        Assert.DoesNotContain(results, r => r.OrganizationId == organization.Id);

        // Annul
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteAsync(user);
    }

    [Theory, DatabaseData]
    public async Task DoesNotReturnDetails_ForOtherUsers(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var targetUser = await userRepository.CreateTestUserAsync();
        var otherUser = await userRepository.CreateTestUserAsync();

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, otherUser);

        // Act
        var results = await organizationUserRepository.GetManyConfirmedAndAcceptedDetailsByUserAsync(targetUser.Id);

        // Assert
        Assert.DoesNotContain(results, r => r.OrganizationId == organization.Id);

        // Annul
        await organizationRepository.DeleteAsync(organization);
        await userRepository.DeleteManyAsync([targetUser, otherUser]);
    }
}
