
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class OrganizationDomainRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetExpiredOrganizationDomainsAsync_ShouldReturnExpiredOrganizationDomains(
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
        await organizationDomainRepository.CreateAsync(organizationDomain1);
        var organization2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            PrivateKey = "privatekey",

        });
        var organizationDomain2 = new OrganizationDomain
        {
            OrganizationId = organization2.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };
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
    public async Task GetExpiredOrganizationDomainsAsync_ShouldReturnNotReturnDomainsPast72Hours(
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

        var pastDateForValidation = DateTime.UtcNow.AddDays(-6).Date;
        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = $"domain{id}@example.com",
            Txt = "btw+12345",
            CreationDate = pastDateForValidation
        };
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
