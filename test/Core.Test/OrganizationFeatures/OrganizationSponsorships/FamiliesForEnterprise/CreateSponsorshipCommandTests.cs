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
    public class CreateSponsorshipCommandTests : FamiliesForEnterpriseTestsBase
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
        public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan,
            Organization org, OrganizationUser orgUser, SutProvider<CreateSponsorshipCommand> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, "test@bitwarden.com"));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
            OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser,
            SutProvider<CreateSponsorshipCommand> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = statusType;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, "test@bitwarden.com"));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task CreateSponsorship_AlreadySponsoring_Throws(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<CreateSponsorshipCommand> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, sponsorship.PlanSponsorshipType.Value, default, default, "test@bitwarden.com"));

            Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        private async Task DoCreateSponsorship_Success(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId, OrganizationSponsorshipOfferTokenable tokenable,
            SutProvider<CreateSponsorshipCommand> sutProvider, string email = "test@bitwarden.com")
        {
            sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
            sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>().Protect(default).ReturnsForAnyArgs(tokenable.ToToken().ToString());
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(default)).Do(callInfo =>
            {
                var sponsorship = callInfo.Arg<OrganizationSponsorship>();
                sponsorship.Id = sponsorshipId;
            });

            await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, email);
        }

        [Theory]
        [BitAutoData]
        public async Task CreateSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId, OrganizationSponsorshipOfferTokenable tokenable,
            SutProvider<CreateSponsorshipCommand> sutProvider)
        {
            await DoCreateSponsorship_Success(sponsoringOrg, sponsoringOrgUser, sponsoredEmail, friendlyName, sponsorshipId, tokenable, sutProvider);

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
        public async Task CreateSponsorship_SendsSponsorshipOfferEmail(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId, OrganizationSponsorshipOfferTokenable tokenable,
            SutProvider<CreateSponsorshipCommand> sutProvider)
        {
            const string email = "test@bitwarden.com";

            await DoCreateSponsorship_Success(sponsoringOrg, sponsoringOrgUser, sponsoredEmail, friendlyName, sponsorshipId, tokenable, sutProvider, email);

            await sutProvider.GetDependency<IMailService>().Received(1).
                SendFamiliesForEnterpriseOfferEmailAsync(sponsoredEmail, email,
                false, Arg.Any<string>());
        }

        [Theory]
        [BitAutoData]
        public async Task CreateSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
    string sponsoredEmail, string friendlyName, SutProvider<CreateSponsorshipCommand> sutProvider)
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
                sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                    PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, "test@bitwarden.com"));
            Assert.Same(expectedException, actualException);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .DeleteAsync(createdSponsorship);
        }
    }
}
