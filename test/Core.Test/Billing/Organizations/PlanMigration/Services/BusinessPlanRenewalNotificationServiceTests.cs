using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Services;

[SutProviderCustomize]
public class BusinessPlanRenewalNotificationServiceTests
{
    private static Subscription SubscriptionWithPeriodEnd(DateTime? periodEnd) => new()
    {
        Id = "sub_1",
        Items = new StripeList<SubscriptionItem>
        {
            Data = periodEnd is null
                ? []
                : [new SubscriptionItem { CurrentPeriodEnd = periodEnd.Value }]
        }
    };

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCohortIsNull_ReturnsFalseAndSendsNothing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization)
    {
        var result = await sutProvider.Sut.SendRenewalEmailAsync(
            organization, SubscriptionWithPeriodEnd(DateTime.UtcNow.AddDays(30)), cohort: null);

        Assert.False(result);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCohortHasNoMigrationPathId_ReturnsFalseAndSendsNothing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        cohort.MigrationPathId = null;

        var result = await sutProvider.Sut.SendRenewalEmailAsync(
            organization, SubscriptionWithPeriodEnd(DateTime.UtcNow.AddDays(30)), cohort);

        Assert.False(result);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenMigrationPathIdIsUnknown_ReturnsFalseAndSendsNothing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A byte value with no registered MigrationPaths.FromId match.
        cohort.MigrationPathId = (MigrationPathId)byte.MaxValue;

        var result = await sutProvider.Sut.SendRenewalEmailAsync(
            organization, SubscriptionWithPeriodEnd(DateTime.UtcNow.AddDays(30)), cohort);

        Assert.False(result);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenRenewalDateIsIndeterminate_ReturnsFalseAndSendsNothing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A valid, matched migration path so we pass the cohort checks; no subscription item means
        // GetCurrentPeriodEnd() is null.
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;

        var result = await sutProvider.Sut.SendRenewalEmailAsync(
            organization, SubscriptionWithPeriodEnd(periodEnd: null), cohort);

        Assert.False(result);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenValid_SendsEmailWithPlanDerivedQuote_ReturnsTrue(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = new Subscription
        {
            Id = "sub_1",
            CustomerId = "cus_1",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = new DateTime(2026, 9, 1),
                        Quantity = 5,
                        Price = new Price { Id = sourcePlan.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Discounts = []
        };
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var result = await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        Assert.True(result);
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.RenewalDate == "September 1, 2026" &&
                mail.View.Seats == 5 &&
                mail.View.IsAnnual == targetPlan.IsAnnual));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenFixedDiscountExceedsTotal_ClampsToZeroAndLogsWarning(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "big-coupon";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("big-coupon")
            .Returns(new Coupon { Id = "big-coupon", AmountOff = 100_000_00 }); // $100,000 off
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var subscription = new Subscription
        {
            Id = "sub_1",
            CustomerId = "cus_1",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodEnd = new DateTime(2026, 9, 1),
                        Quantity = 1,
                        Price = new Price { Id = sourcePlan.PasswordManager.StripeSeatPlanId }
                    }
                ]
            },
            Discounts = []
        };

        var result = await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        Assert.True(result);
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.TotalPrice == "$0" || mail.View.TotalPrice == "$0.00"));
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received().Log(
            LogLevel.Warning, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
