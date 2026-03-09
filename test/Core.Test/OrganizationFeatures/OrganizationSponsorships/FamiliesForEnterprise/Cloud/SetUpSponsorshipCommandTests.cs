using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

[SutProviderCustomize]
[OrganizationSponsorshipCustomize]
public class SetUpSponsorshipCommandTests : FamiliesForEnterpriseTestsBase
{
    [Theory]
    [BitAutoData]
    public async Task SetUpSponsorship_SponsorshipNotFound_ThrowsBadRequest(Organization org,
        SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SetUpSponsorshipAsync(null, org));

        Assert.Contains("No unredeemed sponsorship offer exists for you.", exception.Message);
        await AssertDidNotSetUpAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SetUpSponsorship_OrgAlreadySponsored_ThrowsBadRequest(Organization org,
        OrganizationSponsorship sponsorship, OrganizationSponsorship existingSponsorship,
        SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoredOrganizationIdAsync(org.Id).Returns(existingSponsorship);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

        Assert.Contains("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.", exception.Message);
        await AssertDidNotSetUpAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(FamiliesPlanTypes))]
    public async Task SetUpSponsorship_TooLongSinceLastSync_ThrowsBadRequest(PlanType planType, Organization org,
        OrganizationSponsorship sponsorship,
        SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        org.PlanType = planType;
        sponsorship.LastSyncDate = DateTime.UtcNow.AddDays(-365);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

        Assert.Contains("This sponsorship offer is more than 6 months old and has expired.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .DeleteAsync(sponsorship);
        await AssertDidNotSetUpAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(NonFamiliesPlanTypes))]
    public async Task SetUpSponsorship_OrgNotFamilies_ThrowsBadRequest(PlanType planType,
        OrganizationSponsorship sponsorship, Organization org,
        SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        org.PlanType = planType;
        sponsorship.LastSyncDate = DateTime.UtcNow;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

        Assert.Contains("Can only redeem sponsorship offer on families organizations.", exception.Message);
        await AssertDidNotSetUpAsync(sutProvider);
    }

    [Theory]
    [BitMemberAutoData(nameof(FamiliesPlanTypes))]
    public async Task SetUpSponsorship_FeatureFlagOff_UsesSponsorOrganizationAsync(PlanType planType,
        OrganizationSponsorship sponsorship, Organization org,
        SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        org.PlanType = planType;
        sponsorship.LastSyncDate = DateTime.UtcNow;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand)
            .Returns(false);

        await sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org);

        await sutProvider.GetDependency<IStripePaymentService>()
            .Received(1)
            .SponsorOrganizationAsync(org, sponsorship);
        await sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .Run(default, default);
        await AssertDidSetUpAsync(sutProvider, sponsorship, org);
    }

    [Theory]
    [BitMemberAutoData(nameof(FamiliesPlanTypes))]
    public async Task SetUpSponsorship_FeatureFlagOn_UsesUpdateOrganizationSubscriptionCommand(PlanType planType,
        OrganizationSponsorship sponsorship, Organization org,
        SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        org.PlanType = planType;
        sponsorship.LastSyncDate = DateTime.UtcNow;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand)
            .Returns(true);

        var existingPlan = MockPlans.Get(planType);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(planType)
            .Returns(existingPlan);

        var expectedPeriodEnd = DateTime.UtcNow.AddYears(1);
        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = expectedPeriodEnd }]
            }
        };
        BillingCommandResult<Subscription> successResult = subscription;
        sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Run(org, Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(successResult);

        await sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org);

        await sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Received(1)
            .Run(org, Arg.Any<OrganizationSubscriptionChangeSet>());
        await sutProvider.GetDependency<IStripePaymentService>()
            .DidNotReceiveWithAnyArgs()
            .SponsorOrganizationAsync(default, default);
        Assert.Equal(expectedPeriodEnd, org.ExpirationDate);
        Assert.Equal(expectedPeriodEnd, sponsorship.ValidUntil);
        await AssertDidSetUpAsync(sutProvider, sponsorship, org);
    }

    private static async Task AssertDidNotSetUpAsync(SutProvider<SetUpSponsorshipCommand> sutProvider)
    {
        await sutProvider.GetDependency<IStripePaymentService>()
            .DidNotReceiveWithAnyArgs()
            .SponsorOrganizationAsync(default, default);
        await sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .Run(default, default);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    private static async Task AssertDidSetUpAsync(SutProvider<SetUpSponsorshipCommand> sutProvider,
        OrganizationSponsorship sponsorship, Organization org)
    {
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpsertAsync(org);
        Assert.Equal(org.Id, sponsorship.SponsoredOrganizationId);
        Assert.Null(sponsorship.OfferedToEmail);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .Received(1)
            .UpsertAsync(sponsorship);
    }
}
