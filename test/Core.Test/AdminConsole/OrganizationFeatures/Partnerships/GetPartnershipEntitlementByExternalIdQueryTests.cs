using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

[SutProviderCustomize]
public class GetPartnershipEntitlementByExternalIdQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_EntitlementExists_ReturnsIt(
        OrganizationPartnershipEntitlement entitlement,
        SutProvider<GetPartnershipEntitlementByExternalIdQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>()
            .GetByExternalIdAsync(entitlement.OrganizationPartnershipId, entitlement.ExternalId)
            .Returns(entitlement);

        var result = await sutProvider.Sut.GetAsync(entitlement.OrganizationPartnershipId, entitlement.ExternalId);

        Assert.Same(entitlement, result.AsSuccess);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_EntitlementMissing_ReturnsNotFound(
        Guid organizationPartnershipId, string externalId,
        SutProvider<GetPartnershipEntitlementByExternalIdQuery> sutProvider)
    {
        var result = await sutProvider.Sut.GetAsync(organizationPartnershipId, externalId);

        Assert.Equal("not_found", Assert.IsType<EntitlementNotFound>(result.AsError).Code);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("P|protected")]
    public async Task GetAsync_InvalidExternalId_ReturnsInvalidExternalIdWithoutLookup(
        string externalId, Guid organizationPartnershipId,
        SutProvider<GetPartnershipEntitlementByExternalIdQuery> sutProvider)
    {
        var result = await sutProvider.Sut.GetAsync(organizationPartnershipId, externalId);

        Assert.IsType<InvalidExternalId>(result.AsError);
        await sutProvider.GetDependency<IOrganizationPartnershipEntitlementRepository>().DidNotReceive()
            .GetByExternalIdAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }
}
