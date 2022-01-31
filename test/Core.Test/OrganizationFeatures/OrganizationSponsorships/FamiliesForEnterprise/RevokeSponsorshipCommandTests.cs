using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    [SutProviderCustomize]
    public class RevokeSponsorshipCommandTests : CancelSponsorshipCommandTestsBase
    {
        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_NoExistingSponsorship_ThrowsBadRequest(Organization org,
    SutProvider<RevokeSponsorshipCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorshipAsync(org, null));

            Assert.Contains("You are not currently sponsoring an organization.", exception.Message);
            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_SponsorshipNotRedeemed_DeletesSponsorship(Organization org,
            OrganizationSponsorship sponsorship, SutProvider<RevokeSponsorshipCommand> sutProvider)
        {
            sponsorship.SponsoredOrganizationId = null;

            await sutProvider.Sut.RevokeSponsorshipAsync(org, sponsorship);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_SponsoredOrgNotFound_ThrowsBadRequest(OrganizationSponsorship sponsorship,
            SutProvider<RevokeSponsorshipCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorshipAsync(null, sponsorship));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

    }
}
