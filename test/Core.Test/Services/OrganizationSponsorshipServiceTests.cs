using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Services
{
    [SutProviderCustomize]
    public class OrganizationSponsorshipServiceTests
    {
        private bool sponsorshipValidator(OrganizationSponsorship sponsorship, OrganizationSponsorship expectedSponsorship)
        {
            try
            {
                AssertHelper.AssertPropertyEqual(sponsorship, expectedSponsorship, nameof(OrganizationSponsorship.Id));
                return true;
            }
            catch
            {
                return false;
            }
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail);

            var expectedSponsorship = new OrganizationSponsorship
            {
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                OfferedToEmail = sponsoredEmail,
                CloudSponsor = true,
            };

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .CreateAsync(Arg.Is<OrganizationSponsorship>(s => sponsorshipValidator(s, expectedSponsorship)));
            // TODO: Validate email called with appropriate token.s
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var expectedException = new Exception();
            OrganizationSponsorship createdSponsorship = null;
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().CreateAsync(default).ThrowsForAnyArgs(callInfo =>
            {
                createdSponsorship = callInfo.ArgAt<OrganizationSponsorship>(0);
                createdSponsorship.Id = Guid.NewGuid();
                return expectedException;
            });

            var actualException = await Assert.ThrowsAsync<Exception>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                    PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail));
            Assert.Same(expectedException, actualException);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .DeleteAsync(createdSponsorship);
        }
    }
}
