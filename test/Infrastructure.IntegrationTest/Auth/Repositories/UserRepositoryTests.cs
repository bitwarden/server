using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class UserRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_Works(IUserRepository userRepository)
    {
        var user = await userRepository.CreateAsync(
            new User
            {
                Name = "Test User",
                Email = $"test+{Guid.NewGuid()}@example.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            }
        );

        await userRepository.DeleteAsync(user);

        var deletedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.Null(deletedUser);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_Works(
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository
    )
    {
        var user1 = await userRepository.CreateAsync(
            new User
            {
                Name = "Test User 1",
                Email = $"test+{Guid.NewGuid()}@email.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            }
        );

        var user2 = await userRepository.CreateAsync(
            new User
            {
                Name = "Test User 2",
                Email = $"test+{Guid.NewGuid()}@email.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            }
        );

        var user3 = await userRepository.CreateAsync(
            new User
            {
                Name = "Test User 3",
                Email = $"test+{Guid.NewGuid()}@email.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            }
        );

        var organization = await organizationRepository.CreateAsync(
            new Organization
            {
                Name = "Test Org",
                BillingEmail = user3.Email, // TODO: EF does not enfore this being NOT NULL
                Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            }
        );

        await organizationUserRepository.CreateAsync(
            new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = user1.Id,
                Status = OrganizationUserStatusType.Confirmed,
            }
        );

        await organizationUserRepository.CreateAsync(
            new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = user3.Id,
                Status = OrganizationUserStatusType.Confirmed,
            }
        );

        await userRepository.DeleteManyAsync(new List<User> { user1, user2 });

        var deletedUser1 = await userRepository.GetByIdAsync(user1.Id);
        var deletedUser2 = await userRepository.GetByIdAsync(user2.Id);
        var notDeletedUser3 = await userRepository.GetByIdAsync(user3.Id);

        var orgUser1Deleted = await organizationUserRepository.GetByIdAsync(user1.Id);

        var notDeletedOrgUsers = await organizationUserRepository.GetManyByUserAsync(user3.Id);

        Assert.Null(deletedUser1);
        Assert.Null(deletedUser2);
        Assert.NotNull(notDeletedUser3);

        Assert.Null(orgUser1Deleted);
        Assert.NotNull(notDeletedOrgUsers);
        Assert.True(notDeletedOrgUsers.Count > 0);
    }
}
