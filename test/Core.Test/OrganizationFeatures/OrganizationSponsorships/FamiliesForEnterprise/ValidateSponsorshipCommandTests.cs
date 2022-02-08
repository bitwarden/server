using System;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise
{
    [SutProviderCustomize]
    public class ValidateSponsorshipCommandTests : CancelSponsorshipCommandTestsBase
    {
        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_NoSponsoredOrg_EarlyReturn(Guid sponsoredOrgId,
            SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            SutProvider<ValidateSponsorshipCommand> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, null, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            OrganizationSponsorship existingSponsorship, SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            OrganizationSponsorship existingSponsorship, SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            OrganizationSponsorship existingSponsorship, SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            SutProvider<ValidateSponsorshipCommand> sutProvider)
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
            SutProvider<ValidateSponsorshipCommand> sutProvider)
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
    }
}
