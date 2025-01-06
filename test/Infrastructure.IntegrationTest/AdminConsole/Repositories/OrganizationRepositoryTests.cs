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

        var user1Response = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user1.Id);
        var user2Response = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user2.Id);
        var user3Response = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user3.Id);

        Assert.NotEmpty(user1Response);
        Assert.Equal(organization.Id, user1Response.First().Id);
        Assert.Empty(user2Response);
        Assert.Empty(user3Response);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithUnverifiedDomains_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{id}@{domainName}",
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
            BillingEmail = user.Email,
            Plan = "Test",
            PrivateKey = "privatekey",
        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain.SetNextRunDate(12);
        organizationDomain.SetJobRunCount();
        await organizationDomainRepository.CreateAsync(organizationDomain);

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey",
        });

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        Assert.Empty(result);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithMultipleVerifiedDomains_ReturnsAllMatchingOrganizations(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org 1 {id}",
            BillingEmail = user.Email,
            Plan = "Test",
            PrivateKey = "privatekey1",
        });

        var organization2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org 2 {id}",
            BillingEmail = user.Email,
            Plan = "Test",
            PrivateKey = "privatekey2",
        });

        var organizationDomain1 = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain1.SetNextRunDate(12);
        organizationDomain1.SetJobRunCount();
        organizationDomain1.SetVerifiedDate();
        await organizationDomainRepository.CreateAsync(organizationDomain1);

        var organizationDomain2 = new OrganizationDomain
        {
            OrganizationId = organization2.Id,
            DomainName = domainName,
            Txt = "btw+67890",
        };
        organizationDomain2.SetNextRunDate(12);
        organizationDomain2.SetJobRunCount();
        organizationDomain2.SetVerifiedDate();
        await organizationDomainRepository.CreateAsync(organizationDomain2);

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization1.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization2.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey2",
        });

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, org => org.Id == organization1.Id);
        Assert.Contains(result, org => org.Id == organization2.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithNonExistentUser_ReturnsEmpty(
        IOrganizationRepository organizationRepository)
    {
        var nonExistentUserId = Guid.NewGuid();

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(nonExistentUserId);

        Assert.Empty(result);
    }
}
