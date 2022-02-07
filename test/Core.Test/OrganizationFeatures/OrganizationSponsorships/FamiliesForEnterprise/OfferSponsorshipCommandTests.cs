using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.Core;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    [SutProviderCustomize]
    public class OfferSponsorshipCommandTests : FamiliesForEnterpriseTestsBase
    {
        private bool SponsorshipValidator(OrganizationSponsorship sponsorship, OrganizationSponsorship expectedSponsorship)
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

        [Theory, BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task OfferSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan,
            Organization org, OrganizationUser orgUser, SutProvider<OfferSponsorshipCommand> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, "test@bitwarden.com"));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task OfferSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
            OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser,
            SutProvider<OfferSponsorshipCommand> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = statusType;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, "test@bitwarden.com"));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_AlreadySponsoring_Throws(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<OfferSponsorshipCommand> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(org, orgUser, sponsorship.PlanSponsorshipType.Value, default, default, "test@bitwarden.com"));

            Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        private async Task DoOfferSponsorship_Success(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId, OrganizationSponsorshipOfferTokenable tokenable,
            SutProvider<OfferSponsorshipCommand> sutProvider, string email = "test@bitwarden.com")
        {
            sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
            sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>().Protect(default).ReturnsForAnyArgs(tokenable.ToToken().ToString());
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(default)).Do(callInfo =>
            {
                var sponsorship = callInfo.Arg<OrganizationSponsorship>();
                sponsorship.Id = sponsorshipId;
            });

            await sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, email);
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId, OrganizationSponsorshipOfferTokenable tokenable,
            SutProvider<OfferSponsorshipCommand> sutProvider)
        {
            await DoOfferSponsorship_Success(sponsoringOrg, sponsoringOrgUser, sponsoredEmail, friendlyName, sponsorshipId, tokenable, sutProvider);

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
                .UpsertAsync(Arg.Is<OrganizationSponsorship>(s => SponsorshipValidator(s, expectedSponsorship)));
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_SendsSponsorshipOfferEmail(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId, OrganizationSponsorshipOfferTokenable tokenable,
            SutProvider<OfferSponsorshipCommand> sutProvider)
        {
            const string email = "test@bitwarden.com";

            await DoOfferSponsorship_Success(sponsoringOrg, sponsoringOrgUser, sponsoredEmail, friendlyName, sponsorshipId, tokenable, sutProvider, email);

            await sutProvider.GetDependency<IMailService>().Received(1).
                SendFamiliesForEnterpriseOfferEmailAsync(sponsoredEmail, email,
                false, Arg.Any<string>());
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
    string sponsoredEmail, string friendlyName, SutProvider<OfferSponsorshipCommand> sutProvider)
        {
            sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
            sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

            var expectedException = new Exception();
            OrganizationSponsorship createdSponsorship = null;
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().UpsertAsync(default).ThrowsForAnyArgs(callInfo =>
            {
                createdSponsorship = callInfo.ArgAt<OrganizationSponsorship>(0);
                createdSponsorship.Id = Guid.NewGuid();
                return expectedException;
            });

            var actualException = await Assert.ThrowsAsync<Exception>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                    PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, "test@bitwarden.com"));
            Assert.Same(expectedException, actualException);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .DeleteAsync(createdSponsorship);
        }
    }
}
