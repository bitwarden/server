
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
    public class RemoveSponsorshipCommandTests : CancelSponsorshipCommandTestsBase
    {
        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsoredOrgNull_ThrowsBadRequest(OrganizationSponsorship sponsorship,
    SutProvider<RemoveSponsorshipCommand> sutProvider)
        {
            sponsorship.SponsoredOrganizationId = null;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(null, sponsorship));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsorshipNotFound_ThrowsBadRequest(Organization sponsoredOrg,
            SutProvider<RemoveSponsorshipCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg, null));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsoredOrgNotFound_ThrowsBadRequest(OrganizationSponsorship sponsorship,
            SutProvider<RemoveSponsorshipCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(null, sponsorship));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }
    }
}
