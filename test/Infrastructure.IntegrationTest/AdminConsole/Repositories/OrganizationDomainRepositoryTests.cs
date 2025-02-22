﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

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

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByNextRunDateAsync_ShouldReturnUnverifiedDomains(
     IOrganizationRepository organizationRepository,
     IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var id = Guid.NewGuid();

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = $"test+{id}@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };

        var within36HoursWindow = 1;
        organizationDomain.SetNextRunDate(within36HoursWindow);

        await organizationDomainRepository.CreateAsync(organizationDomain);

        var date = organizationDomain.NextRunDate;

        // Act
        var domains = await organizationDomainRepository.GetManyByNextRunDateAsync(date);

        // Assert
        var expectedDomain = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain.DomainName);
        Assert.NotNull(expectedDomain);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByNextRunDateAsync_ShouldNotReturnUnverifiedDomains_WhenNextRunDateIsOutside36hoursWindow(
        IOrganizationRepository organizationRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var id = Guid.NewGuid();

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = $"test+{id}@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };

        var outside36HoursWindow = 50;
        organizationDomain.SetNextRunDate(outside36HoursWindow);

        await organizationDomainRepository.CreateAsync(organizationDomain);

        var date = DateTime.UtcNow.AddDays(1);

        // Act
        var domains = await organizationDomainRepository.GetManyByNextRunDateAsync(date);

        // Assert
        var expectedDomain = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain.DomainName);
        Assert.Null(expectedDomain);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByNextRunDateAsync_ShouldNotReturnVerifiedDomains(
        IOrganizationRepository organizationRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var id = Guid.NewGuid();

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = $"test+{id}@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain2+{id}@example.com",
            Txt = "btw+12345"
        };

        var within36HoursWindow = 1;
        organizationDomain.SetNextRunDate(within36HoursWindow);
        organizationDomain.SetVerifiedDate();

        await organizationDomainRepository.CreateAsync(organizationDomain);

        var date = DateTimeOffset.UtcNow.Date.AddDays(1);

        // Act
        var domains = await organizationDomainRepository.GetManyByNextRunDateAsync(date);

        // Assert
        var expectedDomain = domains.FirstOrDefault(domain => domain.DomainName == organizationDomain.DomainName);
        Assert.Null(expectedDomain);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetVerifiedDomainsByOrganizationIdsAsync_ShouldVerifiedDomainsMatchesOrganizationIds(
        IOrganizationRepository organizationRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var organization1 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {guid1}",
            BillingEmail = $"test+{guid1}@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organization1Domain1 = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain1+{guid1}@example.com",
            Txt = "btw+12345"
        };

        const int arbitraryNextIteration = 1;
        organization1Domain1.SetNextRunDate(arbitraryNextIteration);
        organization1Domain1.SetVerifiedDate();

        await organizationDomainRepository.CreateAsync(organization1Domain1);

        var organization1Domain2 = new OrganizationDomain
        {
            OrganizationId = organization1.Id,
            DomainName = $"domain2+{guid1}@example.com",
            Txt = "btw+12345"
        };

        organization1Domain2.SetNextRunDate(arbitraryNextIteration);

        await organizationDomainRepository.CreateAsync(organization1Domain2);

        var organization2 = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {guid2}",
            BillingEmail = $"test+{guid2}@example.com",
            Plan = "Test",
            PrivateKey = "privatekey",

        });

        var organization2Domain1 = new OrganizationDomain
        {
            OrganizationId = organization2.Id,
            DomainName = $"domain+{guid2}@example.com",
            Txt = "btw+12345"
        };
        organization2Domain1.SetVerifiedDate();
        organization2Domain1.SetNextRunDate(arbitraryNextIteration);

        await organizationDomainRepository.CreateAsync(organization2Domain1);


        // Act
        var domains = await organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(new[] { organization1.Id });

        // Assert
        var expectedDomain = domains.FirstOrDefault(domain => domain.DomainName == organization1Domain1.DomainName);
        Assert.NotNull(expectedDomain);

        var unverifiedDomain = domains.FirstOrDefault(domain => domain.DomainName == organization1Domain2.DomainName);
        var otherOrganizationDomain = domains.FirstOrDefault(domain => domain.DomainName == organization2Domain1.DomainName);

        Assert.Null(otherOrganizationDomain);
        Assert.Null(unverifiedDomain);
    }
}
