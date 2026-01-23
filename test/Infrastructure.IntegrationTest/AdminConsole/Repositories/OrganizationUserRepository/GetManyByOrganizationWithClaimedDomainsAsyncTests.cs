using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetManyByOrganizationWithClaimedDomainsAsyncTests
{
    [Theory, DatabaseData]
    public async Task WithVerifiedDomain_WithOneMatchingEmailDomain_ReturnsSingle(
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
            Name = "Test User 3",
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

        var orgUser1 = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user1);
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user2);
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user3);

        var result = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(orgUser1.Id, result.Single().Id);
    }

    [Theory, DatabaseData]
    public async Task WithNoVerifiedDomain_ReturnsEmpty(
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

        // Create domain but do NOT verify it
        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain.SetNextRunDate(12);
        // Note: NOT calling SetVerifiedDate()
        await organizationDomainRepository.CreateAsync(organizationDomain);

        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);

        var result = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(result);
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
    public async Task WithVerifiedDomain_ExcludesInvitedUsers(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";

        var invitedUser = await userRepository.CreateAsync(new User
        {
            Name = "Invited User",
            Email = $"invited+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var confirmedUser = await userRepository.CreateAsync(new User
        {
            Name = "Confirmed User",
            Email = $"confirmed+{id}@{domainName}",
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

        // Create invited user with UserId set (edge case - should be excluded even with UserId linked)
        var invitedOrgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = invitedUser.Id, // Edge case: invited user with UserId set
            Email = invitedUser.Email,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User
        });

        // Create confirmed user linked by UserId only (no Email field set)
        var confirmedOrgUser = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, confirmedUser);

        var result = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(result);
        var claimedUser = Assert.Single(result);
        Assert.Equal(confirmedOrgUser.Id, claimedUser.Id);
    }
}
