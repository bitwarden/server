using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using NSubstitute;
using Stripe;
using Xunit;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class InvoicePaymentPaidHandlerTests
{
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService =
        Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();
    private readonly IStripeEventService _stripeEventService = Substitute.For<IStripeEventService>();
    private readonly INonChargeCashApplicationRecorder _recorder = Substitute.For<INonChargeCashApplicationRecorder>();
    private readonly InvoicePaymentPaidHandler _sut;

    public InvoicePaymentPaidHandlerTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions).Returns(true);
        _sut = new InvoicePaymentPaidHandler(_featureService, _stripeEventService, _recorder);
    }

    [Fact]
    public async Task HandleAsync_PaymentRecord_FetchesWithInvoiceAndRecords()
    {
        var eventInvoicePayment = InvoicePayment("payment_record");
        var freshInvoicePayment = InvoicePayment("payment_record");
        _stripeEventService.GetInvoicePayment(Arg.Any<Event>()).Returns(eventInvoicePayment);
        _stripeEventService.GetInvoicePayment(Arg.Any<Event>(), true, Arg.Any<List<string>>()).Returns(freshInvoicePayment);

        await _sut.HandleAsync(new Event());

        await _stripeEventService.Received(1).GetInvoicePayment(Arg.Any<Event>(), true,
            Arg.Is<List<string>>(expand => expand.Contains("invoice")));
        await _recorder.Received(1).RecordAsync(freshInvoicePayment);
    }

    /// <summary>
    /// Most invoice payments are card charges; the event payload rules them out with no Stripe call.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CardPaymentInEventPayload_NoFetchAndNothingRecorded()
    {
        _stripeEventService.GetInvoicePayment(Arg.Any<Event>()).Returns(InvoicePayment("payment_intent"));

        await _sut.HandleAsync(new Event());

        await _stripeEventService.DidNotReceive().GetInvoicePayment(Arg.Any<Event>(), true, Arg.Any<List<string>>());
        await _recorder.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    [Fact]
    public async Task HandleAsync_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions).Returns(false);

        await _sut.HandleAsync(new Event());

        await _stripeEventService.DidNotReceiveWithAnyArgs().GetInvoicePayment(default!);
        await _recorder.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    private static InvoicePayment InvoicePayment(string paymentType) => new()
    {
        Id = "inpay_123",
        Status = "paid",
        Payment = new InvoicePaymentPayment { Type = paymentType }
    };
}
