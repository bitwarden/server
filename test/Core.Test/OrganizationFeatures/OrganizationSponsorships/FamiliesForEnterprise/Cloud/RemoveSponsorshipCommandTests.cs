
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
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
                sutProvider.Sut.RemoveSponsorshipAsync(sponsorship));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);
            //TODO MDG: Assert not marked for deletion
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsorshipNotFound_ThrowsBadRequest(SutProvider<RemoveSponsorshipCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(null));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);
            //TODO MDG: Assert not marked for deletion
        }
    }
}
