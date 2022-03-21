using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Core.IntegrationTest
{
    public class UserActions
    {
        [RequireDatabaseTheory, RepositoryData]
        public async Task NewUser_CreatesUser(IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository)
        {
            await using var userItem = await TemporaryItem.CreateAsync(async () =>
            {
                return await userRepository.CreateAsync(new User
                {
                    Name = "Test User",
                    Email = "test@email.com",
                    EmailVerified = true,
                    MasterPassword = "AQAAAAEAAYagAAAAEE0oseTjAdjsaIuRwApXa4yJgD2qAUzjlt3WV1McP62Qz9LGwgwqHEHsMoR54NaKOw==", // "Password"
                    MasterPasswordHint = null,
                    Culture = "en-US",
                    SecurityStamp = "ZY3CQZAEJZVG2U4TC7DMEFEYLSELQA6I",
                    AccountRevisionDate = DateTime.UtcNow,
                    Key = "2.something",
                    PublicKey = "sjkfal;jdfa",
                    PrivateKey = "2.something",
                    ApiKey = "TESTAPIKEY",
                });
            },
            async (user) =>
            {
                await userRepository.DeleteAsync(user);
            });

            await using var orgItem = await TemporaryItem.CreateAsync(async () =>
            {
                return await organizationRepository.CreateAsync(new Organization
                {
                    Name = "Test Organization",
                    BillingEmail = "test@email.com",
                    Plan = "Enterprise (Annually)",
                });
            },
            async (org) =>
            {
                await organizationRepository.DeleteAsync(org);
            });

            await using var orgUserItem = await TemporaryItem.CreateAsync(async () =>
            {
                return await organizationUserRepository.CreateAsync(new OrganizationUser
                {
                    OrganizationId = orgItem.Item.Id,
                    UserId = userItem.Item.Id,
                });
            },
            async (orgUser) =>
            {
                await organizationUserRepository.DeleteAsync(orgUser);
            });
        }
    }
}
