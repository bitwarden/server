using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
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

        public static IEnumerable<object[]> EnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product == ProductType.Enterprise).Select(p => new object[] { p });

        public static IEnumerable<object[]> NonEnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Enterprise).Select(p => new object[] { p });

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
        public async Task RemoveSponsorshipAsync_NullDoNothing(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(null, null);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_NullSponsoredOrg(OrganizationSponsorship sponsorship,
   SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(null, sponsorship);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_NullSponsorship(Organization sponsoredOrg,
    SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg, null);

            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, null, sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_RemoveBoth(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg, sponsorship);

            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, sponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }
    }
}
