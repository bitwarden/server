using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Services
{
    [SutProviderCustomize]
    public class OrganizationSponsorshipServiceTests
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

        public static IEnumerable<object[]> EnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product == ProductType.Enterprise).Select(p => new object[] { p });

        public static IEnumerable<object[]> NonEnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Enterprise).Select(p => new object[] { p });

        public static IEnumerable<object[]> NonFamiliesPlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Families).Select(p => new object[] { p });

        public static IEnumerable<object[]> NonConfirmedOrganizationUsersStatuses =>
            Enum.GetValues<OrganizationUserStatusType>()
        .Where(s => s != OrganizationUserStatusType.Confirmed)
        .Select(s => new object[] { s });

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task OfferSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan,
            Organization org, OrganizationUser orgUser, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, "test@bitwarden.com"));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
            OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = statusType;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

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
            SutProvider<OrganizationSponsorshipService> sutProvider)
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

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            const string email = "test@bitwarden.com";

            sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
            sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

            var dataProtector = Substitute.For<IDataProtector>();
            sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector(default).ReturnsForAnyArgs(dataProtector);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().CreateAsync(default).ReturnsForAnyArgs(callInfo =>
            {
                var sponsorship = callInfo.Arg<OrganizationSponsorship>();
                sponsorship.Id = sponsorshipId;
                return sponsorship;
            });

            await sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, email);

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

            await sutProvider.GetDependency<IMailService>().Received(1).
                SendFamiliesForEnterpriseOfferEmailAsync(sponsoredEmail, email,
                false, Arg.Any<string>());
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, SutProvider<OrganizationSponsorshipService> sutProvider)
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

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_SponsoringOrgNotFound_ThrowsBadRequest(
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.ResendSponsorshipOfferAsync(null, orgUser, sponsorship, "test@bitwarden.com"));

            Assert.Contains("Cannot find the requested sponsoring organization.", exception.Message);
            await sutProvider.GetDependency<IMailService>()
                .DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseOfferEmailAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_SponsoringOrgUserNotFound_ThrowsBadRequest(Organization org,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.ResendSponsorshipOfferAsync(org, null, sponsorship, "test@bitwarden.com"));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IMailService>()
                .DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseOfferEmailAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task ResendSponsorshipOffer_SponsoringOrgUserNotConfirmed_ThrowsBadRequest(OrganizationUserStatusType status,
            Organization org, OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            orgUser.Status = status;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.ResendSponsorshipOfferAsync(org, orgUser, sponsorship, "test@bitwarden.com"));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IMailService>()
                .DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseOfferEmailAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_SponsorshipNotFound_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.ResendSponsorshipOfferAsync(org, orgUser, null, "test@bitwarden.com"));

            Assert.Contains("Cannot find an outstanding sponsorship offer for this organization.", exception.Message);
            await sutProvider.GetDependency<IMailService>()
                .DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseOfferEmailAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOffer_NoOfferToEmail_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            orgUser.Status = OrganizationUserStatusType.Confirmed;
            sponsorship.OfferedToEmail = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.ResendSponsorshipOfferAsync(org, orgUser, sponsorship, "test@bitwarden.com"));

            Assert.Contains("Cannot find an outstanding sponsorship offer for this organization.", exception.Message);
            await sutProvider.GetDependency<IMailService>()
                .DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseOfferEmailAsync(default, default, default, default);
        }


        [Theory]
        [BitAutoData]
        public async Task SendSponsorshipOfferAsync(OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            const string email = "test@bitwarden.com";

            sutProvider.GetDependency<IUserRepository>()
                .GetByEmailAsync(sponsorship.OfferedToEmail)
                .Returns(Task.FromResult(new User()));

            await sutProvider.Sut.SendSponsorshipOfferAsync(sponsorship, email);

            await sutProvider.GetDependency<IMailService>().Received(1)
                .SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, email, true, Arg.Any<string>());
        }

        [Theory]
        [BitAutoData]
        public async Task SetUpSponsorship_SponsorshipNotFound_ThrowsBadRequest(Organization org,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SetUpSponsorshipAsync(null, org));

            Assert.Contains("No unredeemed sponsorship offer exists for you.", exception.Message);
            await sutProvider.GetDependency<IPaymentService>()
                .DidNotReceiveWithAnyArgs()
                .SponsorOrganizationAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SetUpSponsorship_OrgAlreadySponsored_ThrowsBadRequest(Organization org,
            OrganizationSponsorship sponsorship, OrganizationSponsorship existingSponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(org.Id).Returns(existingSponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

            Assert.Contains("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.", exception.Message);
            await sutProvider.GetDependency<IPaymentService>()
                .DidNotReceiveWithAnyArgs()
                .SponsorOrganizationAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonFamiliesPlanTypes))]
        public async Task SetUpSponsorship_OrgNotFamiles_ThrowsBadRequest(PlanType planType,
            OrganizationSponsorship sponsorship, Organization org,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            org.PlanType = planType;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

            Assert.Contains("Can only redeem sponsorship offer on families organizations.", exception.Message);
            await sutProvider.GetDependency<IPaymentService>()
                .DidNotReceiveWithAnyArgs()
                .SponsorOrganizationAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        private async Task AssertRemovedSponsoredPaymentAsync(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .RemoveOrganizationSponsorshipAsync(sponsoredOrg, sponsorship);
            await sutProvider.GetDependency<IOrganizationRepository>().Received(1).UpsertAsync(sponsoredOrg);
            await sutProvider.GetDependency<IMailService>().Received(1)
                .SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(sponsoredOrg.BillingEmailAddress(), sponsoredOrg.Name);
        }

        private async Task AssertRemovedSponsorshipAsync(OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            if (sponsorship.CloudSponsor || sponsorship.SponsorshipLapsedDate.HasValue)
            {
                await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                    .DeleteAsync(sponsorship);
            }
            else
            {
                await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                    .UpsertAsync(sponsorship);
            }
        }

        private static async Task AssertDidNotRemoveSponsoredPaymentAsync(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.GetDependency<IPaymentService>().DidNotReceiveWithAnyArgs()
                .RemoveOrganizationSponsorshipAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(default, default);
        }

        private static async Task AssertDidNotRemoveSponsorshipAsync(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .DeleteAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_NoSponsoredOrg_EarlyReturn(Guid sponsoredOrgId,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrgId).Returns((Organization)null);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrgId);

            Assert.False(result);
            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_NoExistingSponsorship_UpdatesStripePlan(Organization sponsoredOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, null, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            existingSponsorship.SponsoringOrganizationId = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgUserNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            existingSponsorship.SponsoringOrganizationUserId = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsorshipTypeNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            existingSponsorship.PlanSponsorshipType = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNotFound_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNotEnterprise_UpdatesStripePlan(PlanType planType,
            Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task ValidateSponsorshipAsync_SponsoringOrgDisabled_UpdatesStripePlan(PlanType planType,
            Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsoringOrg.Enabled = false;
            existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task ValidateSponsorshipAsync_Valid(PlanType planType,
            Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsoringOrg.Enabled = true;
            existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.True(result);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_NoExistingSponsorship_ThrowsBadRequest(Organization org,
            SutProvider<OrganizationSponsorshipService> sutProvider)
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
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsorship.SponsoredOrganizationId = null;

            await sutProvider.Sut.RevokeSponsorshipAsync(org, sponsorship);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_SponsoredOrgNotFound_ThrowsBadRequest(OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorshipAsync(null, sponsorship));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsoredOrgNull_ThrowsBadRequest(OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
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
            SutProvider<OrganizationSponsorshipService> sutProvider)
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
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(null, sponsorship));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task DoRemoveSponsorshipAsync_NullDoNothing(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.DoRemoveSponsorshipAsync(null, null);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task DoRemoveSponsorshipAsync_NullSponsoredOrg(OrganizationSponsorship sponsorship,
   SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.DoRemoveSponsorshipAsync(null, sponsorship);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task DoRemoveSponsorshipAsync_NullSponsorship(Organization sponsoredOrg,
    SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.DoRemoveSponsorshipAsync(sponsoredOrg, null);

            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, null, sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task DoRemoveSponsorshipAsync_RemoveBoth(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.DoRemoveSponsorshipAsync(sponsoredOrg, sponsorship);

            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, sponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }
    }
}
