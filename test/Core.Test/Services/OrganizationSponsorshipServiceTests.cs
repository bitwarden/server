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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Extensions;
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
            string sponsoredEmail, string friendlyName, Guid sponsorshipId,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var dataProtector = Substitute.For<IDataProtector>();
            sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector(default).ReturnsForAnyArgs(dataProtector);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().CreateAsync(default).ReturnsForAnyArgs(callInfo =>
            {
                var sponsorship = callInfo.Arg<OrganizationSponsorship>();
                sponsorship.Id = sponsorshipId;
                return sponsorship;
            });

            await sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName);

            var expectedSponsorship = new OrganizationSponsorship
            {
                Id = sponsorshipId,
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
                CloudSponsor = true,
            };

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .CreateAsync(Arg.Is<OrganizationSponsorship>(s => sponsorshipValidator(s, expectedSponsorship)));

            await sutProvider.GetDependency<IMailService>().Received(1).
                SendFamiliesForEnterpriseOfferEmailAsync(sponsoredEmail, sponsoringOrg.Name,
                Arg.Any<string>());
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, SutProvider<OrganizationSponsorshipService> sutProvider)
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
                    PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName));
            Assert.Same(expectedException, actualException);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .DeleteAsync(createdSponsorship);
        }

        [Theory]
        [BitAutoData]
        public async Task SendSponsorshipOfferAsync(Organization org, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.SendSponsorshipOfferAsync(org, sponsorship);

            await sutProvider.GetDependency<IMailService>().Received(1)
                .SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, org.Name, Arg.Any<string>());
        }

        // TODO: test validateSponsorshipAsync

        // TODO: test RemoveSponsorshipAsync
    }
}
