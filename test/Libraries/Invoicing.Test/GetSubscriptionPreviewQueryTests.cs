using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class GetSubscriptionPreviewQueryTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IInvoicePreviewService _invoicePreviewService = Substitute.For<IInvoicePreviewService>();
    private readonly GetSubscriptionPreviewQuery _sut;

    public GetSubscriptionPreviewQueryTests() =>
        _sut = new GetSubscriptionPreviewQuery(
            new RecordingLogger<GetSubscriptionPreviewQuery>(), _pricingClient, _stripeAdapter, _invoicePreviewService);

    [Fact]
    public async Task Run_WithoutGatewaySubscriptionId_ReturnsNull()
    {
        var result = await _sut.Run(new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = null });

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_WhenSubscriptionNotFound_ReturnsNull()
    {
        var organization = new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_missing" };
        _stripeAdapter.GetSubscriptionAsync("sub_missing", Arg.Any<SubscriptionGetOptions>())
            .Throws(StripeError(StripeConstants.ErrorCodes.ResourceMissing));

        var result = await _sut.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_ActiveOrganization_ProjectsPreviewWithTierCadenceAndNextCharge()
    {
        var periodEnd = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewaySubscriptionId = "sub_active",
            PlanType = PlanType.TeamsAnnually
        };
        _stripeAdapter.GetSubscriptionAsync("sub_active", Arg.Any<SubscriptionGetOptions>())
            .Returns(Subscription("sub_active", "active", periodEnd));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TestPlan(ProductTierType.Teams, isAnnual: true));
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Teams, PlanCadenceType.Annually)
            .Returns(SampleInvoicePreview());

        var result = await _sut.Run(organization);

        Assert.NotNull(result);
        Assert.Equal("active", result!.Status);
        Assert.Equal(periodEnd, result.InvoicePreview.NextPaymentAttempt);
    }

    [Fact]
    public async Task Run_TeamsStarterPlan_CollapsesToTeamsTier()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewaySubscriptionId = "sub_starter",
            PlanType = PlanType.TeamsStarter
        };
        _stripeAdapter.GetSubscriptionAsync("sub_starter", Arg.Any<SubscriptionGetOptions>())
            .Returns(Subscription("sub_starter", "active", new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsStarter).Returns(new TestPlan(ProductTierType.TeamsStarter, isAnnual: false));
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        await _sut.Run(organization);

        await _invoicePreviewService.Received(1)
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Teams, PlanCadenceType.Monthly);
    }

    [Fact]
    public async Task Run_CanceledOrganization_FallsBackToSubscriptionProjection()
    {
        var canceledAt = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewaySubscriptionId = "sub_canceled",
            PlanType = PlanType.EnterpriseAnnually
        };
        _stripeAdapter.GetSubscriptionAsync("sub_canceled", Arg.Any<SubscriptionGetOptions>())
            .Returns(CanceledSubscription("sub_canceled", canceledAt));
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan(ProductTierType.Enterprise, isAnnual: true));
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Throws(StripeError(StripeConstants.ErrorCodes.InvoiceUpcomingNone));
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<Subscription>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        var result = await _sut.Run(organization);

        Assert.NotNull(result);
        Assert.Equal("canceled", result!.Status);
        Assert.Equal(canceledAt, result.Canceled);
        Assert.Null(result.InvoicePreview.NextPaymentAttempt);
        await _invoicePreviewService.Received(1)
            .GetInvoicePreviewAsync(Arg.Any<Subscription>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>());
    }

    [Fact]
    public async Task Run_IncompleteOrganization_SetsInitialSuspensionAndGracePeriod()
    {
        var created = new DateTime(2027, 1, 10, 8, 0, 0, DateTimeKind.Utc);
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewaySubscriptionId = "sub_incomplete",
            PlanType = PlanType.TeamsAnnually
        };
        _stripeAdapter.GetSubscriptionAsync("sub_incomplete", Arg.Any<SubscriptionGetOptions>())
            .Returns(IncompleteSubscription("sub_incomplete", created));
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TestPlan(ProductTierType.Teams, isAnnual: true));
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), Arg.Any<PlanTierType>(), Arg.Any<PlanCadenceType>())
            .Returns(SampleInvoicePreview());

        var result = await _sut.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(created.AddHours(23), result!.Suspension);
        Assert.Equal(1, result.GracePeriod);
    }

    [Fact]
    public async Task Run_UnsupportedSubscriberType_ThrowsConflictException()
    {
        var provider = new Provider { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_provider" };
        _stripeAdapter.GetSubscriptionAsync("sub_provider", Arg.Any<SubscriptionGetOptions>())
            .Returns(Subscription("sub_provider", "active", new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)));

        var exception = await Assert.ThrowsAsync<Core.Exceptions.ConflictException>(() => _sut.Run(provider));

        var expected = $"Cannot build a subscription preview for a {provider.SubscriberType()} subscriber.";
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Run_UnsupportedPlanTier_ThrowsConflictException()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewaySubscriptionId = "sub_free",
            PlanType = PlanType.Free
        };
        _stripeAdapter.GetSubscriptionAsync("sub_free", Arg.Any<SubscriptionGetOptions>())
            .Returns(Subscription("sub_free", "active", new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        var testPlan = new TestPlan(ProductTierType.Free, isAnnual: false);
        _pricingClient.GetPlanOrThrow(PlanType.Free).Returns(testPlan);

        var exception = await Assert.ThrowsAsync<Core.Exceptions.ConflictException>(() => _sut.Run(organization));

        var expected = $"Organization ({organization.Id}) plan tier ({testPlan.ProductTier}) has no cart to preview.";
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public async Task Run_UserSubscriber_UsesPremiumAnnualWithoutPricingLookup()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_user" };
        _stripeAdapter.GetSubscriptionAsync("sub_user", Arg.Any<SubscriptionGetOptions>())
            .Returns(Subscription("sub_user", "active", new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        _invoicePreviewService
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Premium, PlanCadenceType.Annually)
            .Returns(SampleInvoicePreview());

        await _sut.Run(user);

        await _pricingClient.DidNotReceive().GetPlanOrThrow(Arg.Any<PlanType>());
        await _invoicePreviewService.Received(1)
            .GetInvoicePreviewAsync(Arg.Any<InvoiceCreatePreviewOptions>(), PlanTierType.Premium, PlanCadenceType.Annually);
    }

    private static StripeException StripeError(string code) =>
        new(System.Net.HttpStatusCode.BadRequest, new StripeError { Code = code }, code);

    private static Subscription Subscription(string id, string status, DateTime periodEnd) =>
        Stripe.Subscription.FromJson($$"""
        {
          "id": "{{id}}", "status": "{{status}}",
          "items": { "data": [
            { "current_period_end": {{new DateTimeOffset(periodEnd).ToUnixTimeSeconds()}},
              "quantity": 5, "price": { "id": "price_pm", "unit_amount": 2558, "metadata": { "purchasable_reference": "pm-seat" } } }
          ] }
        }
        """);

    private static Subscription CanceledSubscription(string id, DateTime canceledAt) =>
        Stripe.Subscription.FromJson($$"""
        {
          "id": "{{id}}", "status": "canceled",
          "canceled_at": {{new DateTimeOffset(canceledAt).ToUnixTimeSeconds()}},
          "items": { "data": [
            { "quantity": 5, "price": { "id": "price_pm", "unit_amount": 2558, "metadata": { "purchasable_reference": "pm-seat" } } }
          ] }
        }
        """);

    private static Subscription IncompleteSubscription(string id, DateTime created) =>
        Stripe.Subscription.FromJson($$"""
        {
          "id": "{{id}}", "status": "incomplete",
          "created": {{new DateTimeOffset(created).ToUnixTimeSeconds()}},
          "items": { "data": [
            { "quantity": 5, "price": { "id": "price_pm", "unit_amount": 2558, "metadata": { "purchasable_reference": "pm-seat" } } }
          ] }
        }
        """);

    private static InvoicePreview SampleInvoicePreview() => new()
    {
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 5, Cost = 100m }
        },
        Cadence = PlanCadenceType.Annually,
        PlanTier = PlanTierType.Teams,
        EstimatedTax = 0m,
        Total = 100m,
        AmountDue = 100m
    };

    // Plan is abstract with protected-init setters and no production concrete subclass (real plans come from
    // the pricing service); this local double sets just the fields the query reads, keeping the test decoupled from Core.Test's plan mocks.
    private sealed record TestPlan : Bit.Core.Models.StaticStore.Plan
    {
        public TestPlan(ProductTierType productTier, bool isAnnual)
        {
            ProductTier = productTier;
            IsAnnual = isAnnual;
        }
    }
}
