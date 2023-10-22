using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class GetOrganizationDomainByIdOrganizationIdQueryTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationDomainByIdAndOrganizationIdAsync_WithExistingParameters_ReturnsExpectedEntity(
        OrganizationDomain organizationDomain, SutProvider<GetOrganizationDomainByIdOrganizationIdQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .Returns(organizationDomain);

        var result = await sutProvider.Sut.GetOrganizationDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId);

        Assert.Equal(organizationDomain, result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDomainByIdAndOrganizationIdAsync_WithNonExistingParameters_ReturnsNull(
        Guid id, Guid organizationId, OrganizationDomain organizationDomain,
        SutProvider<GetOrganizationDomainByIdOrganizationIdQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .Returns(organizationDomain);

        var result = await sutProvider.Sut.GetOrganizationDomainByIdOrganizationIdAsync(id, organizationId);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetDomainByIdOrganizationIdAsync(id, organizationId);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDomainByIdAndOrganizationIdAsync_WithNonExistingId_ReturnsNull(
        Guid id, OrganizationDomain organizationDomain,
        SutProvider<GetOrganizationDomainByIdOrganizationIdQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .Returns(organizationDomain);

        var result = await sutProvider.Sut.GetOrganizationDomainByIdOrganizationIdAsync(id, organizationDomain.OrganizationId);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetDomainByIdOrganizationIdAsync(id, organizationDomain.OrganizationId);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDomainByIdAndOrganizationIdAsync_WithNonExistingOrgId_ReturnsNull(
        Guid organizationId, OrganizationDomain organizationDomain,
        SutProvider<GetOrganizationDomainByIdOrganizationIdQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .Returns(organizationDomain);

        var result = await sutProvider.Sut.GetOrganizationDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationId);

        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationId);

        Assert.Null(result);
    }
}
