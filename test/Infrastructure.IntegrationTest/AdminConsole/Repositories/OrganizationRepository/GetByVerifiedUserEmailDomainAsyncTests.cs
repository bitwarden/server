using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationRepository;

public class GetByVerifiedUserEmailDomainAsyncTests
{
    [Theory, DatabaseData]
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

        var organization = await organizationRepository.CreateTestOrganizationAsync();

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

        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user1);
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user2);
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user3);

        var user1Response = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user1.Id);
        var user2Response = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user2.Id);
        var user3Response = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user3.Id);

        Assert.NotEmpty(user1Response);
        Assert.Equal(organization.Id, user1Response.First().Id);
        Assert.Empty(user2Response);
        Assert.Empty(user3Response);
    }

    [Theory, DatabaseData]
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

        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain.SetNextRunDate(12);
        organizationDomain.SetJobRunCount();
        await organizationDomainRepository.CreateAsync(organizationDomain);

        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        Assert.Empty(result);
    }

    [Theory, DatabaseData]
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

        var organization1 = await organizationRepository.CreateTestOrganizationAsync();
        var organization2 = await organizationRepository.CreateTestOrganizationAsync();

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

        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization1, user);
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization2, user);

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, org => org.Id == organization1.Id);
        Assert.Contains(result, org => org.Id == organization2.Id);
    }

    [Theory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithNonExistentUser_ReturnsEmpty(
        IOrganizationRepository organizationRepository)
    {
        var nonExistentUserId = Guid.NewGuid();

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(nonExistentUserId);

        Assert.Empty(result);
    }

    /// <summary>
    /// Tests an edge case where some invited users are created linked to a UserId.
    /// This is defective behavior, but will take longer to fix - for now, we are defensive and expressly
    /// exclude such users from the results without relying on the inner join only.
    /// Invited-revoked users linked to a UserId remain intentionally unhandled for now as they have not caused
    /// any issues to date and we want to minimize edge cases.
    /// We will fix the underlying issue going forward: https://bitwarden.atlassian.net/browse/PM-22405
    /// </summary>
    [Theory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithInvitedUserWithUserId_ReturnsEmpty(
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

        var organization = await organizationRepository.CreateTestOrganizationAsync();

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

        // Create invited user with matching email domain but UserId set (edge case)
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Email = user.Email,
            Status = OrganizationUserStatusType.Invited,
        });

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        // Invited users should be excluded even if they have UserId set
        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithAcceptedUser_ReturnsOrganization(
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

        var organization = await organizationRepository.CreateTestOrganizationAsync();

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

        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(organization, user);

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        Assert.NotEmpty(result);
        Assert.Equal(organization.Id, result.First().Id);
    }

    [Theory, DatabaseData]
    public async Task GetByVerifiedUserEmailDomainAsync_WithRevokedUser_ReturnsOrganization(
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

        var organization = await organizationRepository.CreateTestOrganizationAsync();

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

        await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(organization, user);

        var result = await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        Assert.NotEmpty(result);
        Assert.Equal(organization.Id, result.First().Id);
    }
}
