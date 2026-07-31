using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    public async Task SendRenewalEmailAsync_WhenRenewalDateIsIndeterminate_LogsErrorAndSendsNothing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A valid, matched migration path so we pass the cohort checks; no subscription item means
        // GetCurrentPeriodEnd() is null.
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;

        var result = await sutProvider.Sut.SendRenewalEmailAsync(
            organization, SubscriptionWithPeriodEnd(periodEnd: null), cohort);

        // We must skip the email (no blank-date send) AND log at Error — the migration is already
        // committed, so an indeterminate renewal date needs alerting (same severity as a post-schedule
        // send failure), not just a silent skip.
        Assert.False(result);
        await sutProvider.GetDependency<IMailer>().DidNotReceiveWithAnyArgs()
            .SendEmail(Arg.Any<BusinessPlanRenewal2020MigrationMail>());
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("indeterminate")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
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

        // The fixed coupon is larger than the whole seat total, so the computed price goes negative. We
        // clamp the displayed total to $0, but a paid migration quoting $0 usually means a misconfigured
        // (too-large) coupon, so we also log a warning to surface it for investigation.
        Assert.True(result);
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail => mail.View.TotalPrice == "$0"));
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("went below zero after discounts")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- Discount resolution -------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WithPercentageCohortCoupon_ItemizesDiscount_AndSuppressesFullPrice(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "loyalty-20";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("loyalty-20")
            .Returns(new Coupon { PercentOff = 20 });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        var result = await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        Assert.True(result);
        // EnterprisePlan annual SeatPrice is $72.00; per-user monthly renders as SeatPrice/12 = $6 (not the
        // $0.00 BasePrice) — asserting this total guards against the BasePrice copy-paste bug. Annual per-year
        // total: 320 x $72 = $23,040 gross; less 20% = $18,432. Whole-dollar amounts render without the trailing
        // .00.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                mail.View.HasDiscount &&
                mail.View.IsAnnual &&
                mail.View.Seats == 320 &&
                mail.View.RenewalDate == "June 12, 2026" &&
                mail.View.PerUserMonthlyPrice == "$6" &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.TotalPrice == "$18,432"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenDiscountedTotalHasCents_RendersTotalWithTwoDecimals(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "loyalty-33";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("loyalty-33")
            .Returns(new Coupon { PercentOff = 33 });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // 320 x $72 = $23,040 gross; less 33% = $15,436.80. The fractional total keeps two decimals; the
        // whole-dollar per-user monthly ($6) drops them.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.IsAnnual &&
                mail.View.Seats == 320 &&
                mail.View.PerUserMonthlyPrice == "$6" &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "33%" &&
                mail.View.TotalPrice == "$15,436.80"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCouponIsAmountOff_SubtractsFixedAmountFromTotal(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "fifty-off";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        // Stripe reports amount-off coupons in minor units (cents): $50.00 off = 5000.
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("fifty-off")
            .Returns(new Coupon { AmountOff = 5000 });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // 320 x $72 = $23,040 gross; less the $50 fixed amount = $22,990. The discount line shows the formatted
        // dollar amount (whole-dollar, so .00 is trimmed), not a percentage.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.IsAnnual &&
                mail.View.Seats == 320 &&
                mail.View.PerUserMonthlyPrice == "$6" &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "$50" &&
                mail.View.TotalPrice == "$22,990"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCohortHasNoCoupon_SendsPriceOnlyRenewalEmail(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Full price, no discount section; annual per-year total: 320 x $72 = $23,040.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                !mail.View.HasDiscount &&
                !mail.View.ShowProactiveDiscountCopy &&
                mail.View.IsAnnual &&
                mail.View.Seats == 320 &&
                mail.View.PerUserMonthlyPrice == "$6" &&
                mail.View.TotalPrice == "$23,040"));
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().GetCouponAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCouponUnresolvable_SendsPriceOnlyRenewalEmail(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "missing-coupon";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("missing-coupon")
            .ThrowsAsync(new StripeException("No such coupon"));
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        // The email is still sent, price-only, and the StripeException does not propagate.
        var result = await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        Assert.True(result);
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                !mail.View.HasDiscount &&
                !mail.View.ShowProactiveDiscountCopy));
    }

    [Theory]
    [BitAutoData("repeating", 12L, 12, true)]
    [BitAutoData("once", null, 0, false)]
    [BitAutoData("forever", null, 0, false)]
    public async Task SendRenewalEmailAsync_SetsProactiveDiscountMonths_FromCouponDuration(
        string duration, long? durationInMonths, int expectedMonths, bool expectedShow,
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A proactive coupon whose Stripe duration drives the loyalty-discount copy. Only a "repeating" coupon
        // has a finite month span; "once"/"forever" suppress the copy.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "loyalty";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("loyalty")
            .Returns(new Coupon { PercentOff = 20, Duration = duration, DurationInMonths = durationInMonths });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.ProactiveDiscountMonths == expectedMonths &&
                mail.View.ShowProactiveDiscountCopy == expectedShow));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCouponHasNeitherPercentNorAmount_SendsPriceOnly_AndLogsError(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "empty-coupon";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        // A coupon that resolves but exposes neither PercentOff nor AmountOff is a cohort misconfiguration.
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("empty-coupon")
            .Returns(new Coupon { PercentOff = null, AmountOff = null });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Email is sent price-only, and the misconfiguration is logged as an error (a coupon with no usable
        // discount mis-quotes every org in the cohort, so it must reach alerting).
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                !mail.View.HasDiscount));
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("neither PercentOff nor AmountOff") &&
                o.ToString()!.Contains("empty-coupon") &&
                o.ToString()!.Contains(organization.Id.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCohortCouponOnPhase_PlusSubscriptionCoupon_ItemizesAndTotalsBoth(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The ticket repro (PM-38729): a 20% cohort coupon carried on the post-renewal schedule phase plus a 5%
        // subscription-level coupon. Both must be itemized and reflected in the total, matching Stripe.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "cohort-20";
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Coupon = new Coupon { Id = "churn-5", PercentOff = 5 } }],
            frozenTime: now);
        StubActiveScheduleWithPhases(sutProvider, subscription, now, futurePhaseCouponId: "cohort-20");
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cohort-20")
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // 320 x $72 = $23,040 gross; discounts compound like Stripe ($23,040 x 0.80 x 0.95 = $17,510.40), not
        // summed to a flat 25%. Cohort line first.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 2 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.DiscountLines[1] == "5%" &&
                mail.View.TotalPrice == "$17,510.40"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenSameCouponInMultipleSources_ResolvesOnceWithoutDoubleSubtracting(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The same coupon id appears as the cohort coupon, a subscription discount, and the phase discount.
        // Dedup must resolve it once, so the total reflects a single 20% reduction.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "loyalty-20";
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Coupon = new Coupon { Id = "loyalty-20", PercentOff = 20 } }],
            frozenTime: now);
        StubActiveScheduleWithPhases(sutProvider, subscription, now, futurePhaseCouponId: "loyalty-20");
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("loyalty-20")
            .Returns(new Coupon { Id = "loyalty-20", PercentOff = 20 });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // One line, single 20% reduction: $23,040 x 0.8 = $18,432 (not double-subtracted). The coupon is fetched
        // exactly once: the phase loop's seen-id short-circuit must skip the already-resolved coupon before
        // re-fetching it, not just dedup on add.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.TotalPrice == "$18,432"));
        await sutProvider.GetDependency<IStripeAdapter>().Received(1).GetCouponAsync("loyalty-20");
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenNoCohortCoupon_ButSubscriptionCoupon_ItemizesSubscriptionDiscount(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // No cohort coupon, but the subscription carries a 10% coupon. Previously this showed nothing; the fix
        // itemizes the subscription discount.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Coupon = new Coupon { Id = "sub-10", PercentOff = 10 } }]);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // One line from the subscription discount: $23,040 x 0.9 = $20,736.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "10%" &&
                mail.View.TotalPrice == "$20,736"));
        // No cohort coupon, so the cohort source never fetches a coupon by code.
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().GetCouponAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenDiscountOnlyOnPostRenewalPhase_ResolvesPhaseCouponById(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // No cohort coupon, no subscription discount; the only discount is on the post-renewal schedule phase,
        // exposed as a CouponId that must be resolved via GetCouponAsync.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId, frozenTime: now);
        StubActiveScheduleWithPhases(sutProvider, subscription, now, futurePhaseCouponId: "phase-15");
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("phase-15")
            .Returns(new Coupon { Id = "phase-15", PercentOff = 15 });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Resolved from the phase coupon id: $23,040 x 0.85 = $19,584.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "15%" &&
                mail.View.TotalPrice == "$19,584"));
        await sutProvider.GetDependency<IStripeAdapter>().Received(1).GetCouponAsync("phase-15");
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenMixedPercentAndFixedAcrossSources_OrdersPercentBeforeFixed_AndTrimsDecimals(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A percentage cohort coupon and a fixed-amount subscription coupon. The cohort source is read first, so
        // the percentage line precedes the fixed line, and the whole-dollar fixed amount trims its trailing .00.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "ten-pct";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("ten-pct")
            .Returns(new Coupon { Id = "ten-pct", PercentOff = 10 });

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            // $100.00 off reported in minor units (cents).
            discounts: [new Discount { Coupon = new Coupon { Id = "hundred-off", AmountOff = 10000 } }]);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Percentage line first (cohort), then the fixed line. Math applies the percentage then subtracts the
        // fixed amount: $23,040 x 0.9 = $20,736; less $100 = $20,636.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 2 &&
                mail.View.DiscountLines[0] == "10%" &&
                mail.View.DiscountLines[1] == "$100" &&
                mail.View.TotalPrice == "$20,636"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenOnceAndForeverCoupons_BothItemizedAndApplied(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A "once" subscription coupon and a "forever" cohort coupon. The locked decision is to match Stripe's
        // upcoming invoice, so both are itemized and applied regardless of duration.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "forever-20";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("forever-20")
            .Returns(new Coupon { Id = "forever-20", PercentOff = 20, Duration = "forever" });

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Coupon = new Coupon { Id = "once-10", PercentOff = 10, Duration = "once" } }]);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Both applied and compounded like Stripe: $23,040 x 0.80 x 0.90 = $16,588.80.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 2 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.DiscountLines[1] == "10%" &&
                mail.View.TotalPrice == "$16,588.80"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenNoDiscountAnywhere_QuotesFullPrice_AndNeverFetchesCoupon(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // No cohort coupon, empty subscription discounts, no schedule. Full price, no discount section, and no
        // coupon fetch occurs.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId, discounts: []);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                !mail.View.HasDiscount &&
                mail.View.TotalPrice == "$23,040"));
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().GetCouponAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenPhaseCouponFetchFails_OmitsThatCoupon_KeepsOthers_AndLogsError(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A cohort coupon resolves, but the post-renewal phase coupon fetch throws. The phase coupon is omitted,
        // the cohort discount is still itemized, an error is logged, and the email is still sent.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "cohort-20";
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId, frozenTime: now);
        StubActiveScheduleWithPhases(sutProvider, subscription, now, futurePhaseCouponId: "phase-missing");
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cohort-20")
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("phase-missing")
            .ThrowsAsync(new StripeException("No such coupon"));

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Only the cohort 20% line survives: $23,040 x 0.8 = $18,432; the email is still sent.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.TotalPrice == "$18,432"));
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Could not retrieve discount coupon") &&
                o.ToString()!.Contains("phase-missing") &&
                o.ToString()!.Contains(organization.Id.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenScheduleListFails_KeepsCohortAndSubscriptionDiscounts_AndLogsError(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The schedule-list call itself throws (distinct from a per-phase coupon fetch failing). The cohort and
        // subscription discounts still resolve, the email is still sent, and the failure is logged so a
        // potentially missed schedule-phase discount reaches alerting.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "cohort-20";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .ThrowsAsync(new StripeException("Stripe API error"));
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cohort-20")
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Coupon = new Coupon { Id = "sub-5", PercentOff = 5 } }]);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Cohort 20% + subscription 5% still compound: $23,040 x 0.80 x 0.95 = $17,510.40; email sent.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 2 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.DiscountLines[1] == "5%" &&
                mail.View.TotalPrice == "$17,510.40"));
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Could not list subscription schedules") &&
                o.ToString()!.Contains(organization.Id.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenSubscriptionDiscountCouponNotExpanded_OmitsIt_AndLogsError(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // A subscription discount is present but its Coupon isn't expanded (a Stripe.Discount exposes the id only
        // via Coupon.Id, so there's nothing to fetch by). The cohort discount still resolves, the email is still
        // sent, and the unexpanded discount is logged rather than silently dropped.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "cohort-20";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cohort-20")
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Id = "di_unexpanded", Coupon = null }]);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Only the cohort 20% applies ($23,040 x 0.80 = $18,432); the unexpanded discount is logged.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.TotalPrice == "$18,432"));
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("has no expanded Coupon") &&
                o.ToString()!.Contains("di_unexpanded") &&
                o.ToString()!.Contains(organization.Id.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenNoActiveSchedule_FallsBackToCohortAndSubscriptionDiscounts(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // No active schedule (empty list). The cohort and subscription discounts still resolve and no exception
        // propagates.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "cohort-20";

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cohort-20")
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        var subscription = BusinessSubscription(
            sourcePlan.PasswordManager.StripeSeatPlanId,
            discounts: [new Discount { Coupon = new Coupon { Id = "sub-5", PercentOff = 5 } }]);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Cohort 20% + subscription 5%, compounded like Stripe: $23,040 x 0.80 x 0.95 = $17,510.40.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 2 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.DiscountLines[1] == "5%" &&
                mail.View.TotalPrice == "$17,510.40"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenScheduleHasCurrentAndPostRenewalPhases_ReadsPostRenewalPhaseDiscount(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // Phase-selection coverage modeling the real migration layout. The schedule has an expired anchor phase
        // (EndDate <= now) that must be filtered out, then the canonical [Phase 1, Phase 2] shape: Phase 1 (ends
        // at the renewal date, still in the future, carries a stale coupon) and Phase 2 (post-renewal, live
        // coupon). We must read Phase 2's discount, proving the "second unexpired phase" selection — not the first
        // unexpired phase, and not a phase the EndDate > now filter should have dropped.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var renewalDate = now.AddMonths(1);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId, frozenTime: now);
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_phase_select",
                        SubscriptionId = subscription.Id,
                        Status = StripeConstants.SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-12),
                                EndDate = now.AddDays(-1),
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "expired-50" }]
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-11),
                                EndDate = renewalDate,
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "stale-50" }]
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = renewalDate,
                                EndDate = renewalDate.AddMonths(12),
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "live-25" }]
                            }
                        ]
                    }
                ]
            });
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("live-25")
            .Returns(new Coupon { Id = "live-25", PercentOff = 25 });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Only Phase 2's live coupon is used ($23,040 x 0.75 = $17,280); the current phase's stale coupon and the
        // expired anchor phase's coupon are never fetched.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "25%" &&
                mail.View.TotalPrice == "$17,280"));
        await sutProvider.GetDependency<IStripeAdapter>().Received(1).GetCouponAsync("live-25");
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().GetCouponAsync("stale-50");
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().GetCouponAsync("expired-50");
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenCustomerLevelDiscountMirroredOntoPhase_ItemizesIt(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The scheduler mirrors customer-level discounts onto the post-renewal phase
        // (PriceIncreaseScheduler.ResolvePhase2ForBusinessAsync), so the email picks it up via the schedule-phase
        // source. We do NOT read the customer discount live via subscriptions.data.customer.discount.coupon: that
        // path exceeds Stripe's 4-level expansion limit and 400s the whole webhook.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId, frozenTime: now);
        StubActiveScheduleWithPhases(sutProvider, subscription, now, futurePhaseCouponId: "cust-10");
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cust-10")
            .Returns(new Coupon { Id = "cust-10", PercentOff = 10 });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // The mirrored customer-level 10% is itemized: $23,040 x 0.90 = $20,736.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "10%" &&
                mail.View.TotalPrice == "$20,736"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenScheduleHasUnexpectedPhaseCount_SkipsPhaseDiscounts_AndLogsWarning(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The schedule has only one unexpired phase (e.g. a webhook race advanced Phase 1 -> Phase 2). The
        // canonical [Phase 1, Phase 2] shape is gone, so we must not read its discounts as if it were the
        // post-renewal phase; we log a warning and still send the email with the cohort discount.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = "cohort-20";
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId, frozenTime: now);
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_one_phase",
                        SubscriptionId = subscription.Id,
                        Status = StripeConstants.SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-11),
                                EndDate = now.AddMonths(1),
                                Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "phase-only" }]
                            }
                        ]
                    }
                ]
            });
        sutProvider.GetDependency<IStripeAdapter>().GetCouponAsync("cohort-20")
            .Returns(new Coupon { Id = "cohort-20", PercentOff = 20 });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Only the cohort 20% applies ($23,040 x 0.80 = $18,432); the lone phase's coupon is not read, and the
        // off-nominal phase count is logged at Warning with the schedule id.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.HasDiscount &&
                mail.View.DiscountLines.Count == 1 &&
                mail.View.DiscountLines[0] == "20%" &&
                mail.View.TotalPrice == "$18,432"));
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceive().GetCouponAsync("phase-only");
        sutProvider.GetDependency<ILogger<BusinessPlanRenewalNotificationService>>().Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("1 unexpired phase") &&
                o.ToString()!.Contains("sched_one_phase")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- Seat count and quote resolution -------------------------------------------------------

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenMonthlyTargetPlan_QuotesMonthlySeatPriceWithoutDividing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The monthly migration path. SeatPrice on a monthly plan is already the per-user monthly figure, so it
        // must NOT be divided by 12, and the total is quoted per month (not annualized). This is the regression
        // guard for the cadence bug the review flagged.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Enterprise2020Plan(isAnnual: false);
        var targetPlan = new EnterprisePlan(isAnnual: false);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseMonthly2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Monthly EnterprisePlan SeatPrice is $7.00 (used as-is, NOT /12). Monthly cohorts are quoted per month:
        // IsAnnual is false and TotalPrice = $7 x 320 = $2,240 (NOT annualized).
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.Seats == 320 &&
                !mail.View.IsAnnual &&
                mail.View.PerUserMonthlyPrice == "$7" &&
                mail.View.TotalPrice == "$2,240"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenTeamsAnnualMigrationPath_QuotesTeamsTargetPlanPricing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The Teams annual migration path. Guards the Teams source/target plan resolution, whose SeatPrice ($48
        // annual) differs from Enterprise ($72), so a swapped From/To mapping would surface here.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Teams2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Teams2020Plan(isAnnual: true);
        var targetPlan = new TeamsPlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Annual TeamsPlan SeatPrice is $48.00; per-user monthly is $48/12 = $4; annual per-year total = $48 x
        // 320 = $15,360. No coupon, so no discount section.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                !mail.View.HasDiscount &&
                mail.View.IsAnnual &&
                mail.View.Seats == 320 &&
                mail.View.PerUserMonthlyPrice == "$4" &&
                mail.View.TotalPrice == "$15,360"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenTeamsMonthlyMigrationPath_QuotesMonthlySeatPriceWithoutDividing(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The Teams monthly migration path. Combines the monthly cadence (SeatPrice used as-is) with the Teams
        // tier, completing the four-path cadence x tier matrix.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Teams2020MonthlyToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Teams2020Plan(isAnnual: false);
        var targetPlan = new TeamsPlan(isAnnual: false);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Monthly TeamsPlan SeatPrice is $5.00 (used as-is, NOT /12). Monthly cohorts are quoted per month:
        // IsAnnual is false and TotalPrice = $5 x 320 = $1,600 (NOT annualized).
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.Seats == 320 &&
                !mail.View.IsAnnual &&
                mail.View.PerUserMonthlyPrice == "$5" &&
                mail.View.TotalPrice == "$1,600"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_WhenSecretsManagerItemsPrecedePasswordManagerSeat_QuotesPasswordManagerSeatCount(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // The subscription carries Secrets Manager seats and service accounts ahead of the password-manager seat
        // line, with different quantities. Stripe does not guarantee item ordering, so the email must resolve
        // seats by matching the source plan's seat price ID, not by taking the first positive-quantity item. This
        // is the regression guard for the seat-count bug the review flagged.
        organization.BillingEmail = "org@example.com";
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Enterprise2020Plan(isAnnual: true);
        var targetPlan = new EnterprisePlan(isAnnual: true);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually2020).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(targetPlan);
        StubNoActiveSchedule(sutProvider);

        // Prepend non-seat lines with quantities a naive "first positive quantity" lookup would grab.
        var periodEnd = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        var subscription = new Subscription
        {
            Id = "sub_business",
            CustomerId = "cus_business",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "secrets-manager-enterprise-seat-annually" },
                        Quantity = 50,
                        CurrentPeriodEnd = periodEnd
                    },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "secrets-manager-service-account-annually" },
                        Quantity = 75,
                        CurrentPeriodEnd = periodEnd
                    },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = "2020-enterprise-org-seat-annually" },
                        Quantity = 320,
                        CurrentPeriodEnd = periodEnd
                    }
                ]
            }
        };

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // The password-manager seat quantity (320) is quoted, NOT the SM-seat (50) or service-account (75)
        // quantity. Annual EnterprisePlan SeatPrice is $72: 320 x $72 = $23,040.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.Seats == 320 &&
                mail.View.IsAnnual &&
                mail.View.PerUserMonthlyPrice == "$6" &&
                mail.View.TotalPrice == "$23,040"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_TeamsStarter_QuotesOccupiedSeatCount(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // PM-37512: the renewal email must quote the org's occupied seat count, not organization.Seats (the
        // bundle cap of 10), and the per-user monthly reflects TeamsMonthly's $5 seat price.
        organization.BillingEmail = "org@example.com";
        organization.Seats = 10;
        cohort.MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new TeamsStarterPlan2023();
        var targetPlan = new TeamsPlan(isAnnual: false);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 7 });
        StubNoActiveSchedule(sutProvider);

        // Teams Starter is a flat bundle with no PM seat line; the seat line here only carries the renewal date.
        var subscription = BusinessSubscription("teams-org-starter");

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // The email quotes 7 occupied seats (not the bundle cap of 10) at the $5 monthly seat price.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                !mail.View.IsAnnual &&
                mail.View.Seats == 7 &&
                mail.View.PerUserMonthlyPrice == "$5"));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_TeamsStarter_RaisesQuotedSeatsToCoverSecretsManager(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // PM-39816: when the org holds more Secrets Manager seats than occupied members, the renewal email must
        // quote the raised Password Manager seat count so it matches what the scheduler bills (SM <= PM). 9 SM
        // seats / 7 occupied members => the email quotes 9, flooring on the Stripe SM line.
        organization.BillingEmail = "org@example.com";
        organization.Seats = 10;
        organization.SmSeats = 9;
        cohort.MigrationPathId = MigrationPathId.TeamsStarter2023ToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new TeamsStarterPlan2023();
        var targetPlan = new TeamsPlan(isAnnual: false);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsStarter2023).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 7 });
        StubNoActiveSchedule(sutProvider);

        // The bundle seat line (renewal date) plus the surviving Stripe SM seat line (9) the scheduler floors on.
        var subscription = BusinessSubscription("teams-org-starter");
        subscription.Items.Data.Add(new SubscriptionItem
        {
            Price = new Price { Id = sourcePlan.SecretsManager.StripeSeatPlanId },
            Quantity = 9
        });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // The email quotes 9 seats (raised from 7 occupied to cover SM), matching the invoice.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                !mail.View.IsAnnual &&
                mail.View.Seats == 9));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_Teams2019_RaisesQuotedSeatsToCoverSecretsManager(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // PM-39816: the renewal email also raises the quoted seat count for the Teams 2019 packaged source,
        // matching the scheduler's floor on the Stripe SM seat line. 9 SM seats / 7 occupied -> quote 9.
        organization.BillingEmail = "org@example.com";
        organization.Seats = 5;
        organization.SmSeats = 9;
        cohort.MigrationPathId = MigrationPathId.Teams2019MonthlyToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Teams2019Plan(isAnnual: false);
        var targetPlan = new TeamsPlan(isAnnual: false);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly2019).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 7 });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);
        subscription.Items.Data.Add(new SubscriptionItem
        {
            Price = new Price { Id = sourcePlan.SecretsManager.StripeSeatPlanId },
            Quantity = 9
        });

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // The email quotes 9 seats (raised from 7 occupied to cover SM), matching the invoice.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.ToEmails.Contains("org@example.com") &&
                mail.View.Seats == 9));
    }

    [Theory, BitAutoData]
    public async Task SendRenewalEmailAsync_Teams2019Migration_SubFiveOrg_QuotesOccupiedSeats(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohort cohort)
    {
        // PM-37514: a Teams 2019 (ActualUsage) renewal email must quote the same seat count the scheduler bills —
        // for a sub-5 org that is the occupied count, NOT organization.Seats (the base allotment) and NOT the
        // seat-overage line. 3 occupied of a 5-base org -> the email quotes 3 seats.
        organization.BillingEmail = "org@example.com";
        organization.Seats = 5; // base allotment; only 3 are occupied
        cohort.MigrationPathId = MigrationPathId.Teams2019MonthlyToCurrent;
        cohort.ProactiveDiscountCouponCode = null;

        var sourcePlan = new Teams2019Plan(isAnnual: false);
        var targetPlan = new TeamsPlan(isAnnual: false);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly2019).Returns(sourcePlan);
        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly).Returns(targetPlan);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 3 });
        StubNoActiveSchedule(sutProvider);

        var subscription = BusinessSubscription(sourcePlan.PasswordManager.StripeSeatPlanId);

        await sutProvider.Sut.SendRenewalEmailAsync(organization, subscription, cohort);

        // Quotes occupied (3), not the 5-seat base allotment.
        await sutProvider.GetDependency<IMailer>().Received(1).SendEmail(
            Arg.Is<BusinessPlanRenewal2020MigrationMail>(mail =>
                mail.View.Seats == 3));
    }

    // --- Helpers -------------------------------------------------------------------------------

    // Builds a subscription mirroring the business-migration fixture: a single seat line (320 seats) whose price
    // id matches the source plan's password-manager seat price, with the renewal date on June 12, 2026.
    private static Subscription BusinessSubscription(
        string seatPriceId,
        List<Discount>? discounts = null,
        DateTime? frozenTime = null)
    {
        var subscription = new Subscription
        {
            Id = "sub_business",
            CustomerId = "cus_business",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Price = new Price { Id = seatPriceId },
                        Quantity = 320,
                        CurrentPeriodEnd = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            },
            Discounts = discounts
        };

        if (frozenTime.HasValue)
        {
            // FrozenTime is what the discount-resolution "now" reads to pick the post-renewal schedule phase.
            subscription.TestClock = new Stripe.TestHelpers.TestClock
            {
                FrozenTime = frozenTime.Value,
                Status = "ready"
            };
        }

        return subscription;
    }

    private static void StubNoActiveSchedule(SutProvider<BusinessPlanRenewalNotificationService> sutProvider) =>
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

    // Registers an active subscription schedule modeling the real migration layout: a current phase that ends at
    // the renewal date (its EndDate is still in the future when the event fires) and a post-renewal phase that
    // starts at the renewal date and carries the given coupon. The renewal-bearing coupon lives only on the
    // post-renewal phase, so reading the current phase (EndDate > now) would miss it.
    private static void StubActiveScheduleWithPhases(
        SutProvider<BusinessPlanRenewalNotificationService> sutProvider,
        Subscription subscription, DateTime now, string futurePhaseCouponId)
    {
        var renewalDate = now.AddMonths(1);
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule>
            {
                Data =
                [
                    new SubscriptionSchedule
                    {
                        Id = "sched_active",
                        SubscriptionId = subscription.Id,
                        Status = StripeConstants.SubscriptionScheduleStatus.Active,
                        Phases =
                        [
                            new SubscriptionSchedulePhase
                            {
                                StartDate = now.AddMonths(-11),
                                EndDate = renewalDate
                            },
                            new SubscriptionSchedulePhase
                            {
                                StartDate = renewalDate,
                                EndDate = renewalDate.AddMonths(12),
                                Discounts =
                                    [new SubscriptionSchedulePhaseDiscount { CouponId = futurePhaseCouponId }]
                            }
                        ]
                    }
                ]
            });
    }
}
