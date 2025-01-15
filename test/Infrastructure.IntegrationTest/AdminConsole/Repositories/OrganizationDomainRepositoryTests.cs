using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class OrganizationDomainRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetExpiredOrganizationDomainsAsync_ShouldReturn3DaysOldUnverifiedDomains(
     IUserRepository userRepository,
     IOrganizationRepository organizationRepository,
     IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var id = Guid.NewGuid();

        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organizationDomain1 = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };
        var dummyInterval = 1;
        organizationDomain1.SetNextRunDate(dummyInterval);

        var beforeValidationDate = DateTime.UtcNow.AddDays(-4).Date;

        await organizationDomainRepository.CreateAsync(organizationDomain1);
        var organization2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            PrivateKey = "privatekey",
            CreationDate = beforeValidationDate
        });
        var organizationDomain2 = new OrganizationDomain
        {
            OrganizationId = organization2.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345",
            CreationDate = beforeValidationDate
        };
        organizationDomain2.SetNextRunDate(dummyInterval);
        await organizationDomainRepository.CreateAsync(organizationDomain2);

        // Act
        var domains = await organizationDomainRepository.GetExpiredOrganizationDomainsAsync();

        // Assert
        var expectedDomain1 = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain1.DomainName);
        Assert.NotNull(expectedDomain1);

        var expectedDomain2 = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain2.DomainName);
        Assert.NotNull(expectedDomain2);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetExpiredOrganizationDomainsAsync_ShouldNotReturnDomainsUnder3DaysOld(
     IUserRepository userRepository,
     IOrganizationRepository organizationRepository,
     IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var id = Guid.NewGuid();

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user.Email,
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var beforeValidationDate = DateTime.UtcNow.AddDays(-1).Date;
        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = $"domain{id}@example.com",
            Txt = "btw+12345",
            CreationDate = beforeValidationDate
        };
        var dummyInterval = 1;
        organizationDomain.SetNextRunDate(dummyInterval);
        await organizationDomainRepository.CreateAsync(organizationDomain);

        // Act
        var domains = await organizationDomainRepository.GetExpiredOrganizationDomainsAsync();

        // Assert
        var expectedDomain2 = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain.DomainName);
        Assert.Null(expectedDomain2);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetExpiredOrganizationDomainsAsync_ShouldNotReturnVerifiedDomains(
     IUserRepository userRepository,
     IOrganizationRepository organizationRepository,
     IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var id = Guid.NewGuid();

        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user.Email,
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organizationDomain1 = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };
        organizationDomain1.SetVerifiedDate();
        var dummyInterval = 1;

        organizationDomain1.SetNextRunDate(dummyInterval);

        await organizationDomainRepository.CreateAsync(organizationDomain1);

        var organization2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user.Email,
            Plan = "Test",
            PrivateKey = "privatekey",
        });

        var organizationDomain2 = new OrganizationDomain
        {
            OrganizationId = organization2.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };
        organizationDomain2.SetNextRunDate(dummyInterval);
        organizationDomain2.SetVerifiedDate();

        await organizationDomainRepository.CreateAsync(organizationDomain2);

        // Act
        var domains = await organizationDomainRepository.GetExpiredOrganizationDomainsAsync();

        // Assert
        var expectedDomain1 = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain1.DomainName);
        Assert.Null(expectedDomain1);

        var expectedDomain2 = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain2.DomainName);
        Assert.Null(expectedDomain2);
    }
}
