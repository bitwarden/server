using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

[SutProviderCustomize]
public class OrganizationSponsorshipRenewCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateExpirationDate_UpdatesValidUntil(OrganizationSponsorship sponsorship, DateTime expireDate,
        SutProvider<OrganizationSponsorshipRenewCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetBySponsoredOrganizationIdAsync(sponsorship.SponsoredOrganizationId.Value).Returns(sponsorship);

        await sutProvider.Sut.UpdateExpirationDateAsync(sponsorship.SponsoredOrganizationId.Value, expireDate);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .UpsertAsync(sponsorship);
    }
}
