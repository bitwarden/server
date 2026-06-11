using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class ConfirmManyOrganizationUsersTests
{
    [Theory, DatabaseData]
    public async Task ConfirmManyOrganizationUsersAsync_MixedBatch_ReturnsOnlyAcceptedIds(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var acceptedUser = await userRepository.CreateTestUserAsync("accepted");
        var confirmedUser = await userRepository.CreateTestUserAsync("confirmed");

        var acceptedOrgUser = await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(org, acceptedUser);
        var confirmedOrgUser = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(org, confirmedUser);

        var usersToConfirm = new[]
        {
            new AcceptedOrganizationUserToConfirm { OrganizationUserId = acceptedOrgUser.Id, UserId = acceptedUser.Id, Key = "key-accepted" },
            new AcceptedOrganizationUserToConfirm { OrganizationUserId = confirmedOrgUser.Id, UserId = confirmedUser.Id, Key = "key-already-confirmed" },
            new AcceptedOrganizationUserToConfirm { OrganizationUserId = Guid.NewGuid(), UserId = Guid.NewGuid(), Key = "key-nonexistent" },
        };

        // Act
        var before = DateTime.UtcNow;
        var confirmedIds = await organizationUserRepository.ConfirmManyOrganizationUsersAsync(usersToConfirm);

        // Assert — only the Accepted user's ID is returned
        Assert.Single(confirmedIds);
        Assert.Contains(acceptedOrgUser.Id, confirmedIds);

        // The previously-accepted user is now Confirmed
        var updated = await organizationUserRepository.GetByIdAsync(acceptedOrgUser.Id);
        Assert.NotNull(updated);
        Assert.Equal(OrganizationUserStatusType.Confirmed, updated.Status);
        Assert.Equal("key-accepted", updated.Key);
        Assert.True(updated.RevisionDate >= before);

        // The already-confirmed user's status is unchanged
        var unchanged = await organizationUserRepository.GetByIdAsync(confirmedOrgUser.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(OrganizationUserStatusType.Confirmed, unchanged.Status);
        Assert.Null(unchanged.Key);
    }

    [Theory, DatabaseData]
    public async Task ConfirmManyOrganizationUsersAsync_Idempotent_SecondCallWithSameBatchReturnsEmpty(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(org, user);

        var batch = new[]
        {
            new AcceptedOrganizationUserToConfirm { OrganizationUserId = orgUser.Id, UserId = user.Id, Key = "key" }
        };

        // Act
        var firstResult = await organizationUserRepository.ConfirmManyOrganizationUsersAsync(batch);
        var secondResult = await organizationUserRepository.ConfirmManyOrganizationUsersAsync(batch);

        // Assert — first call confirms, second call is a no-op (user is no longer Accepted)
        Assert.Single(firstResult);
        Assert.Contains(orgUser.Id, firstResult);
        Assert.Empty(secondResult);

        var finalState = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(finalState);
        Assert.Equal(OrganizationUserStatusType.Confirmed, finalState.Status);
    }
}
