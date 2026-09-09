using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetManyPendingAutoConfirmTests
{
    [Theory, DatabaseData]
    public async Task GetManyPendingAutoConfirmAsync_ReturnsOnlyAcceptedUsersWithUserType(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrg = await organizationRepository.CreateTestOrganizationAsync();

        // Should be returned: Accepted + Type=User + non-null UserId
        var eligibleUser = await userRepository.CreateTestUserAsync("eligible");
        var eligibleOrgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = eligibleUser.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
        });

        // Should NOT be returned: already Confirmed
        var confirmedUser = await userRepository.CreateTestUserAsync("confirmed");
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(org, confirmedUser);

        // Should NOT be returned: Accepted but Type=Owner (not Type=User)
        var acceptedOwner = await userRepository.CreateTestUserAsync("acceptedOwner");
        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(org, acceptedOwner);

        // Should NOT be returned: Accepted + Type=User but UserId is null (invite-only)
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = null,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
        });

        // Should NOT be returned: belongs to a different organization
        var otherOrgUser = await userRepository.CreateTestUserAsync("otherOrg");
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = otherOrg.Id,
            UserId = otherOrgUser.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
        });

        // Act
        var results = await organizationUserRepository.GetManyPendingAutoConfirmAsync(org.Id);

        // Assert — only the eligible user is returned
        Assert.Single(results);
        Assert.Equal(eligibleOrgUser.Id, results.Single().Id);
    }
}
