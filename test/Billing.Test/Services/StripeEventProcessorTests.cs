using Bit.Billing.Constants;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class StripeEventProcessorTests
{
    private readonly IInvoicePaymentPaidHandler _invoicePaymentPaidHandler = Substitute.For<IInvoicePaymentPaidHandler>();
    private readonly IInvoicePaidHandler _invoicePaidHandler = Substitute.For<IInvoicePaidHandler>();
    private readonly IPaymentSucceededHandler _paymentSucceededHandler = Substitute.For<IPaymentSucceededHandler>();
    private readonly StripeEventProcessor _sut;

    public StripeEventProcessorTests()
    {
        _sut = new StripeEventProcessor(
            Substitute.For<ILogger<StripeEventProcessor>>(),
            Substitute.For<ISubscriptionDeletedHandler>(),
            Substitute.For<ISubscriptionUpdatedHandler>(),
            Substitute.For<IUpcomingInvoiceHandler>(),
            Substitute.For<IChargeSucceededHandler>(),
            Substitute.For<IChargeRefundedHandler>(),
            _paymentSucceededHandler,
            Substitute.For<IPaymentFailedHandler>(),
            Substitute.For<IInvoiceCreatedHandler>(),
            Substitute.For<IPaymentMethodAttachedHandler>(),
            Substitute.For<ICustomerUpdatedHandler>(),
            Substitute.For<IInvoiceFinalizedHandler>(),
            Substitute.For<ISetupIntentSucceededHandler>(),
            Substitute.For<ICouponDeletedHandler>(),
            Substitute.For<ICheckoutSessionCompletedHandler>(),
            _invoicePaymentPaidHandler,
            _invoicePaidHandler);
    }

    [Fact]
    public async Task ProcessEventAsync_InvoicePaymentPaid_DispatchesToInvoicePaymentPaidHandler()
    {
        var parsedEvent = new Event { Type = HandledStripeWebhook.InvoicePaymentPaid };

        await _sut.ProcessEventAsync(parsedEvent);

        await _invoicePaymentPaidHandler.Received(1).HandleAsync(parsedEvent);
        await _invoicePaidHandler.DidNotReceiveWithAnyArgs().HandleAsync(default!);
    }

    [Fact]
    public async Task ProcessEventAsync_InvoicePaid_DispatchesToInvoicePaidHandler()
    {
        var parsedEvent = new Event { Type = HandledStripeWebhook.InvoicePaid };

        await _sut.ProcessEventAsync(parsedEvent);

        await _invoicePaidHandler.Received(1).HandleAsync(parsedEvent);
        await _invoicePaymentPaidHandler.DidNotReceiveWithAnyArgs().HandleAsync(default!);
        await _paymentSucceededHandler.DidNotReceiveWithAnyArgs().HandleAsync(default!);
    }
}
