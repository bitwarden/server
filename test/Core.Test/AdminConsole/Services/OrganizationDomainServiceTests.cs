using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

[SutProviderCustomize]
public class OrganizationDomainServiceTests
{

    [Theory, BitAutoData]
    public async Task ValidateOrganizationsDomainAsync_CallsDnsResolverServiceAndReplace(SutProvider<OrganizationDomainService> sutProvider)
    {
        var domains = new List<OrganizationDomain>
        {
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                CreationDate = DateTime.UtcNow,
                DomainName = "test.com",
                Txt = "btw+12345",
            },
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                CreationDate = DateTime.UtcNow,
                DomainName = "test2.com",
                Txt = "btw+6789"
            }
        };
        sutProvider.GetDependency<IOrganizationDomainRepository>().GetManyByNextRunDateAsync(default)
            .ReturnsForAnyArgs(domains);

        await sutProvider.Sut.ValidateOrganizationsDomainAsync();

        await sutProvider.GetDependency<IDnsResolverService>().ReceivedWithAnyArgs(2)
            .ResolveAsync(default, default);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().ReceivedWithAnyArgs(2)
            .ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().ReceivedWithAnyArgs(2)
            .LogOrganizationDomainEventAsync(default, EventType.OrganizationDomain_NotVerified,
                EventSystemUser.DomainVerification);
    }

    [Theory, BitAutoData]
    public async Task OrganizationDomainMaintenanceAsync_CallsDeleteExpiredAsync_WhenExpiredDomainsExist(
        SutProvider<OrganizationDomainService> sutProvider)
    {
        var expiredDomains = new List<OrganizationDomain>
        {
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                CreationDate = DateTime.UtcNow,
                DomainName = "test.com",
                Txt = "btw+12345",
            },
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                CreationDate = DateTime.UtcNow,
                DomainName = "test2.com",
                Txt = "btw+6789"
            }
        };
        sutProvider.GetDependency<IOrganizationDomainRepository>().GetExpiredOrganizationDomainsAsync()
            .Returns(expiredDomains);

        await sutProvider.Sut.OrganizationDomainMaintenanceAsync();

        await sutProvider.GetDependency<IOrganizationDomainRepository>().ReceivedWithAnyArgs(1)
            .DeleteExpiredAsync(7);
    }

    [Theory, BitAutoData]
    public async Task HasVerifiedDomainsAsync_WithVerifiedDomain_ReturnsTrue(
        OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainService> sutProvider)
    {
        organizationDomain.SetVerifiedDate(); // Set the verified date to make it verified

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainsByOrganizationIdAsync(organizationDomain.OrganizationId)
            .Returns(new List<OrganizationDomain> { organizationDomain });

        var result = await sutProvider.Sut.HasVerifiedDomainsAsync(organizationDomain.OrganizationId);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasVerifiedDomainsAsync_WithoutVerifiedDomain_ReturnsFalse(
        OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainsByOrganizationIdAsync(organizationDomain.OrganizationId)
            .Returns(new List<OrganizationDomain> { organizationDomain });

        var result = await sutProvider.Sut.HasVerifiedDomainsAsync(organizationDomain.OrganizationId);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasVerifiedDomainsAsync_WithoutOrganizationDomains_ReturnsFalse(
        Guid organizationId,
        SutProvider<OrganizationDomainService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainsByOrganizationIdAsync(organizationId)
            .Returns(new List<OrganizationDomain>());

        var result = await sutProvider.Sut.HasVerifiedDomainsAsync(organizationId);

        Assert.False(result);
    }
}
