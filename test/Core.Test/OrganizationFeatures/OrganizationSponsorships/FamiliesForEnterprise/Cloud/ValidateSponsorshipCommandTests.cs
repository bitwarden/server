using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

[SutProviderCustomize]
[OrganizationSponsorshipCustomize]
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
        await AssertDidNotDeleteSponsorshipAsync(sutProvider);
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
    public async Task ValidateSponsorshipAsync_SponsoringOrgDefault_UpdatesStripePlan(Organization sponsoredOrg,
        OrganizationSponsorship existingSponsorship, SutProvider<ValidateSponsorshipCommand> sutProvider)
    {
        existingSponsorship.SponsoringOrganizationId = default;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

        var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

        Assert.False(result);
        await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
        await AssertDeletedSponsorshipAsync(existingSponsorship, sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateSponsorshipAsync_SponsoringOrgUserDefault_UpdatesStripePlan(Organization sponsoredOrg,
        OrganizationSponsorship existingSponsorship, SutProvider<ValidateSponsorshipCommand> sutProvider)
    {
        existingSponsorship.SponsoringOrganizationUserId = default;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

        var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

        Assert.False(result);
        await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
        await AssertDeletedSponsorshipAsync(existingSponsorship, sutProvider);
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
        await AssertDeletedSponsorshipAsync(existingSponsorship, sutProvider);
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
        await AssertDeletedSponsorshipAsync(existingSponsorship, sutProvider);
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
        await AssertDeletedSponsorshipAsync(existingSponsorship, sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(EnterprisePlanTypes))]
    public async Task ValidateSponsorshipAsync_SponsoringOrgDisabledLongerThanGrace_UpdatesStripePlan(PlanType planType,
        Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
        SutProvider<ValidateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = planType;
        sponsoringOrg.Enabled = false;
        sponsoringOrg.ExpirationDate = DateTime.UtcNow.AddDays(-100);
        existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

        var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

        Assert.False(result);
        await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
        await AssertDeletedSponsorshipAsync(existingSponsorship, sutProvider);
    }

    [Theory]
    [OrganizationSponsorshipCustomize(ToDelete = true)]
    [BitMemberAutoData(nameof(EnterprisePlanTypes))]
    public async Task ValidateSponsorshipAsync_ToDeleteSponsorship_IsInvalid(PlanType planType,
        Organization sponsoredOrg, OrganizationSponsorship sponsorship, Organization sponsoringOrg,
        SutProvider<ValidateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = planType;
        sponsoringOrg.Enabled = true;
        sponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(sponsorship);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

        var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

        Assert.False(result);

        await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, sponsorship, sutProvider);
        await AssertDeletedSponsorshipAsync(sponsorship, sutProvider);
    }


    [Theory]
    [BitMemberAutoData(nameof(EnterprisePlanTypes))]
    public async Task ValidateSponsorshipAsync_SponsoringOrgDisabledUnknownTime_UpdatesStripePlan(PlanType planType,
        Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
        SutProvider<ValidateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = planType;
        sponsoringOrg.Enabled = false;
        sponsoringOrg.ExpirationDate = null;
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
    public async Task ValidateSponsorshipAsync_SponsoringOrgDisabledLessThanGrace_Valid(PlanType planType,
        Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
        SutProvider<ValidateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = planType;
        sponsoringOrg.Enabled = true;
        sponsoringOrg.ExpirationDate = DateTime.UtcNow.AddDays(-1);
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
        await AssertDidNotDeleteSponsorshipAsync(sutProvider);
    }
}
