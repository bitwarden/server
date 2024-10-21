using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class OrganizationHasVerifiedDomainsQueryTests
{
    [Theory, BitAutoData]
    public async Task HasVerifiedDomainsAsync_WithVerifiedDomain_ReturnsTrue(
        OrganizationDomain organizationDomain,
        SutProvider<OrganizationHasVerifiedDomainsQuery> sutProvider)
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
        SutProvider<OrganizationHasVerifiedDomainsQuery> sutProvider)
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
        SutProvider<OrganizationHasVerifiedDomainsQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainsByOrganizationIdAsync(organizationId)
            .Returns(new List<OrganizationDomain>());

        var result = await sutProvider.Sut.HasVerifiedDomainsAsync(organizationId);

        Assert.False(result);
    }
}
