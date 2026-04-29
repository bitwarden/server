using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class OrganizationUserRevocationTests
{
    [Theory, DatabaseData]
    public async Task RevokeManyAsync_SetsStatusAndReason(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        var orgUser1 = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user1);
        var orgUser2 = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user2);

        // Act
        await organizationUserRepository.RevokeManyAsync(
            [orgUser1.Id, orgUser2.Id],
            RevocationReason.TwoFactorPolicyNonCompliance);

        // Assert
        var updated1 = await organizationUserRepository.GetByIdAsync(orgUser1.Id);
        var updated2 = await organizationUserRepository.GetByIdAsync(orgUser2.Id);
        Assert.Equal(OrganizationUserStatusType.Revoked, updated1!.Status);
        Assert.Equal(RevocationReason.TwoFactorPolicyNonCompliance, updated1.RevocationReason);
        Assert.Equal(OrganizationUserStatusType.Revoked, updated2!.Status);
        Assert.Equal(RevocationReason.TwoFactorPolicyNonCompliance, updated2.RevocationReason);
    }

    [Theory, DatabaseData]
    public async Task RevokeManyAsync_WithManualReason_SetsStatusAndReason(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);

        // Act
        await organizationUserRepository.RevokeManyAsync([orgUser.Id], RevocationReason.Manual);

        // Assert
        var updated = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Revoked, updated!.Status);
        Assert.Equal(RevocationReason.Manual, updated.RevocationReason);
    }

    [Theory, DatabaseData]
    public async Task RestoreManyAsync_ClearsRevocationReason(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange — revoke with a reason
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);
        await organizationUserRepository.RevokeManyAsync([orgUser.Id], RevocationReason.Manual);

        // Act
        await organizationUserRepository.RestoreManyAsync([orgUser.Id], OrganizationUserStatusType.Confirmed);

        // Assert
        var restored = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, restored!.Status);
        Assert.Null(restored.RevocationReason);
    }

    [Theory, DatabaseData]
    public async Task RestoreManyAsync_OnlyRestoresRevokedUsers(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        var orgUser1 = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user1);
        var orgUser2 = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user2);
        await organizationUserRepository.RevokeManyAsync([orgUser1.Id], RevocationReason.Manual);

        // Act — attempt to restore both
        await organizationUserRepository.RestoreManyAsync(
            [orgUser1.Id, orgUser2.Id],
            OrganizationUserStatusType.Confirmed);

        // Assert — only the revoked user should have changed
        var restored = await organizationUserRepository.GetByIdAsync(orgUser1.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, restored!.Status);
        Assert.Null(restored.RevocationReason);

        var unchanged = await organizationUserRepository.GetByIdAsync(orgUser2.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, unchanged!.Status);
        Assert.Null(unchanged.RevocationReason);
    }
}
