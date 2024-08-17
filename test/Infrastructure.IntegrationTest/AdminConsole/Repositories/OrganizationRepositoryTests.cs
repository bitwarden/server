using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class OrganizationRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetByClaimedUserDomainAsync_WithVerifiedDomain_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";

        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{id}@x-{domainName}", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user3 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{id}@{domainName}.example.com", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain.SetVerifiedDate();
        organizationDomain.SetNextRunDate(12);
        organizationDomain.SetJobRunCount();
        await organizationDomainRepository.CreateAsync(organizationDomain);

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user3.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var user1Response = await organizationRepository.GetByClaimedUserDomainAsync(user1.Id);
        var user2Response = await organizationRepository.GetByClaimedUserDomainAsync(user2.Id);
        var user3Response = await organizationRepository.GetByClaimedUserDomainAsync(user3.Id);

        Assert.NotNull(user1Response);
        Assert.Equal(organization.Id, user1Response.Id);
        Assert.Null(user2Response);
        Assert.Null(user3Response);
    }
}
