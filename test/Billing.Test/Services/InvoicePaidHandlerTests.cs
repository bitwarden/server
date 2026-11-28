using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using Event = Stripe.Event;
using PaymentMethodType = Bit.Core.Enums.PaymentMethodType;
using Transaction = Bit.Core.Entities.Transaction;
using TransactionType = Bit.Core.Enums.TransactionType;

namespace Bit.Billing.Test.Services;

public class InvoicePaidHandlerTests
{
    private const string InvoiceId = "in_unlinked";
    private const string CustomerId = "cus_unlinked";

    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService =
        Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();
    private readonly IStripeEventService _stripeEventService = Substitute.For<IStripeEventService>();
    private readonly IStripeFacade _stripeFacade = Substitute.For<IStripeFacade>();
    private readonly INonChargeCashApplicationRecorder _recorder = Substitute.For<INonChargeCashApplicationRecorder>();
    private readonly InvoicePaidHandler _sut;

    public InvoicePaidHandlerTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions).Returns(true);
        _sut = new InvoicePaidHandler(_featureService, _stripeEventService, _stripeFacade, _recorder);
    }

    [Fact]
    public async Task HandleAsync_InvoiceWithoutSubscriptionLink_RecordsEveryPaymentWithInvoiceAttached()
    {
        var invoice = CreateInvoiceWithoutSubscriptionLink(Payment("inpay_1", 5000), Payment("inpay_2", 2200));
        SetupInvoice(invoice);

        await _sut.HandleAsync(new Event());

        await _stripeEventService.Received(1).GetInvoice(Arg.Any<Event>(), true,
            Arg.Is<List<string>>(expand => expand.Contains("payments")));
        await _recorder.Received(1).RecordAsync(Arg.Is<InvoicePayment>(p => p.Id == "inpay_1" && p.Invoice == invoice));
        await _recorder.Received(1).RecordAsync(Arg.Is<InvoicePayment>(p => p.Id == "inpay_2" && p.Invoice == invoice));
    }

    /// <summary>
    /// When Stripe returns only the first page of an invoice's payments, the rest are read so none are missed.
    /// </summary>
    [Fact]
    public async Task HandleAsync_MorePaymentsThanReturned_ReadsAllPages()
    {
        var invoice = CreateInvoiceWithoutSubscriptionLink(Payment("inpay_1", 5000));
        invoice.Payments.HasMore = true;
        SetupInvoice(invoice);
        _stripeFacade.ListInvoicePaymentsAutoPagingAsync(Arg.Is<InvoicePaymentListOptions>(o => o.Invoice == InvoiceId))
            .Returns(new[] { Payment("inpay_1", 5000), Payment("inpay_2", 2200), Payment("inpay_3", 100) }
                .ToAsyncEnumerable());

        await _sut.HandleAsync(new Event());

        await _recorder.Received(3).RecordAsync(Arg.Is<InvoicePayment>(p => p.Invoice == invoice));
        await _recorder.Received(1).RecordAsync(Arg.Is<InvoicePayment>(p => p.Id == "inpay_3"));
    }

    [Fact]
    public async Task HandleAsync_SubscriptionInvoice_NoFetchAndNothingRecorded()
    {
        var invoice = CreateInvoiceWithoutSubscriptionLink(Payment("inpay_1", 7200));
        invoice.Parent = new InvoiceParent
        {
            SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = "sub_1" }
        };
        SetupInvoice(invoice);

        await _sut.HandleAsync(new Event());

        await _stripeEventService.DidNotReceive().GetInvoice(Arg.Any<Event>(), true, Arg.Any<List<string>>());
        await _recorder.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    [Fact]
    public async Task HandleAsync_SettledEntirelyFromCredit_NoFetchAndNothingRecorded()
    {
        var invoice = CreateInvoiceWithoutSubscriptionLink();
        invoice.AmountPaid = 0;
        SetupInvoice(invoice);

        await _sut.HandleAsync(new Event());

        await _stripeEventService.DidNotReceive().GetInvoice(Arg.Any<Event>(), true, Arg.Any<List<string>>());
        await _recorder.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    [Fact]
    public async Task HandleAsync_NoPaymentsReturned_RecordsNothingWithoutThrowing()
    {
        var invoice = CreateInvoiceWithoutSubscriptionLink();
        invoice.Payments = null;
        SetupInvoice(invoice);

        await _sut.HandleAsync(new Event());

        await _recorder.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    [Fact]
    public async Task HandleAsync_FeatureFlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions).Returns(false);
        SetupInvoice(CreateInvoiceWithoutSubscriptionLink(Payment("inpay_1", 15000)));

        await _sut.HandleAsync(new Event());

        await _stripeEventService.DidNotReceiveWithAnyArgs().GetInvoice(default!);
        await _recorder.DidNotReceiveWithAnyArgs().RecordAsync(default!);
    }

    /// <summary>
    /// Through the real recorder: an invoice without a subscription link has no subscription, so the owner comes from
    /// the customer's subscriptions as for card charges (canceled ones skipped; an unpaid one counts).
    /// </summary>
    [Fact]
    public async Task HandleAsync_InvoiceWithoutSubscriptionLink_PostsToOwnerOfCustomersSubscription()
    {
        var transactionRepository = Substitute.For<ITransactionRepository>();
        var recorder = new NonChargeCashApplicationRecorder(Substitute.For<ILogger<NonChargeCashApplicationRecorder>>(),
            Substitute.For<ILoggerFactory>(), _stripeFacade, transactionRepository);
        var sut = new InvoicePaidHandler(_featureService, _stripeEventService, _stripeFacade, recorder);

        var organizationId = Guid.NewGuid();
        SetupInvoice(CreateInvoiceWithoutSubscriptionLink(Payment("inpay_wire", 15000)));
        _stripeFacade.GetPaymentRecord("pr_inpay_wire").Returns(new PaymentRecord
        {
            Id = "pr_inpay_wire",
            CustomerDetails = new PaymentRecordCustomerDetails { Name = "Example Corp" },
            PaymentMethodDetails = new PaymentRecordPaymentMethodDetails
            {
                Type = "custom",
                Custom = new PaymentRecordPaymentMethodDetailsCustom { DisplayName = "WIRE", Type = "custom" }
            },
            ProcessorDetails = new PaymentRecordProcessorDetails { Type = "custom" }
        });
        _stripeFacade.ListSubscriptions(Arg.Is<SubscriptionListOptions>(o => o.Customer == CustomerId))
            .Returns(new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = "sub_old",
                        Status = "canceled",
                        Metadata = new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() }
                    },
                    new Subscription
                    {
                        Id = "sub_current",
                        Status = "unpaid",
                        Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
                    }
                ]
            });

        await sut.HandleAsync(new Event());

        await transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == organizationId &&
            t.UserId == null &&
            t.ProviderId == null &&
            t.Amount == 150M &&
            t.Type == TransactionType.Credit &&
            t.Gateway == GatewayType.Bank &&
            t.GatewayId == "inpay_wire" &&
            t.PaymentMethodType == PaymentMethodType.WireTransfer &&
            t.Details == "In Wire - Example Corp $150.00"));
    }

    /// <summary>
    /// A $5,000 invoice settled by a $4,975 wire and a $25 wire fee: the wire posts at its own amount, and the fee's
    /// existence neither blocks it nor posts a payment of its own.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WirePlusWireFee_PostsOnlyTheWire()
    {
        var transactionRepository = Substitute.For<ITransactionRepository>();
        var recorder = new NonChargeCashApplicationRecorder(Substitute.For<ILogger<NonChargeCashApplicationRecorder>>(),
            Substitute.For<ILoggerFactory>(), _stripeFacade, transactionRepository);
        var sut = new InvoicePaidHandler(_featureService, _stripeEventService, _stripeFacade, recorder);

        var organizationId = Guid.NewGuid();
        var invoice = CreateInvoiceWithoutSubscriptionLink(Payment("inpay_wire", 497500), Payment("inpay_fee", 2500));
        invoice.AmountPaid = 500000;
        SetupInvoice(invoice);
        _stripeFacade.GetPaymentRecord("pr_inpay_wire").Returns(CashApplicationRecord("pr_inpay_wire", "Wire"));
        _stripeFacade.GetPaymentRecord("pr_inpay_fee").Returns(CashApplicationRecord("pr_inpay_fee", "Wire Fee"));
        _stripeFacade.ListSubscriptions(Arg.Is<SubscriptionListOptions>(o => o.Customer == CustomerId))
            .Returns(new StripeList<Subscription>
            {
                Data =
                [
                    new Subscription
                    {
                        Id = "sub_current",
                        Status = "active",
                        Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
                    }
                ]
            });

        await sut.HandleAsync(new Event());

        await transactionRepository.Received(1).CreateAsync(Arg.Any<Transaction>());
        await transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.GatewayId == "inpay_wire" &&
            t.Amount == 4975M &&
            t.OrganizationId == organizationId &&
            t.PaymentMethodType == PaymentMethodType.WireTransfer &&
            t.Details == "In Wire - Example Corp $4,975.00"));
    }

    private static PaymentRecord CashApplicationRecord(string id, string paymentType) => new()
    {
        Id = id,
        CustomerDetails = new PaymentRecordCustomerDetails { Name = "Example Corp" },
        PaymentMethodDetails = new PaymentRecordPaymentMethodDetails
        {
            Type = "custom",
            Custom = new PaymentRecordPaymentMethodDetailsCustom { DisplayName = paymentType, Type = "custom" }
        },
        ProcessorDetails = new PaymentRecordProcessorDetails { Type = "custom" }
    };

    private static InvoicePayment Payment(string id, long amountPaid) => new()
    {
        Id = id,
        Status = "paid",
        Currency = "usd",
        AmountPaid = amountPaid,
        Created = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
        StatusTransitions = new InvoicePaymentStatusTransitions
        {
            PaidAt = new DateTime(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc)
        },
        Payment = new InvoicePaymentPayment { Type = "payment_record", PaymentRecordId = "pr_" + id }
    };

    private static Invoice CreateInvoiceWithoutSubscriptionLink(params InvoicePayment[] payments) => new()
    {
        Id = InvoiceId,
        CustomerId = CustomerId,
        AmountPaid = 15000,
        Parent = null,
        Payments = new StripeList<InvoicePayment> { Data = payments.ToList(), HasMore = false }
    };

    private void SetupInvoice(Invoice invoice) =>
        _stripeEventService.GetInvoice(Arg.Any<Event>(), Arg.Any<bool>(), Arg.Any<List<string>>()).Returns(invoice);
}
