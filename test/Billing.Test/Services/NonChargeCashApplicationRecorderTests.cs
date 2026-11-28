using System.Globalization;
using System.Text.Json;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Billing.Test.Utilities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using PaymentMethodType = Bit.Core.Enums.PaymentMethodType;
using Transaction = Bit.Core.Entities.Transaction;
using TransactionType = Bit.Core.Enums.TransactionType;

namespace Bit.Billing.Test.Services;

public class NonChargeCashApplicationRecorderTests
{
    private const string InvoicePaymentId = "inpay_123";
    private const string PaymentRecordId = "pr_123";
    private const string InvoiceId = "in_123";
    private const string SubscriptionId = "sub_123";
    private const string CustomerId = "cus_123";

    private static readonly DateTime _paidAt = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);

    private readonly IStripeFacade _stripeFacade = Substitute.For<IStripeFacade>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly ILogger<NonChargeCashApplicationRecorder> _logger = Substitute.For<ILogger<NonChargeCashApplicationRecorder>>();
    private readonly ILogger _auditLogger = Substitute.For<ILogger>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly NonChargeCashApplicationRecorder _sut;
    private InvoicePayment _invoicePayment = null!;

    private readonly Guid _organizationId = Guid.NewGuid();

    public NonChargeCashApplicationRecorderTests()
    {
        _loggerFactory.CreateLogger(NonChargeCashApplicationRecorder.AuditLogCategory).Returns(_auditLogger);

        _sut = new NonChargeCashApplicationRecorder(
            _logger,
            _loggerFactory,
            _stripeFacade,
            _transactionRepository);
    }

    /// <summary>
    /// Invoice payments and Payment Records captured from Stripe test mode (API version 2026-06-24.dahlia), with IDs
    /// replaced, URLs removed, and wire references set to Billing Operations' "&lt;date&gt; - &lt;payer&gt;" convention.
    /// </summary>
    [Theory]
    [InlineData("cash-application-dashboard-check.json", 72.00, PaymentMethodType.Check,
        "Check In - Example University #1001 $72.00")]
    [InlineData("cash-application-api-wire.json", 100.00, PaymentMethodType.WireTransfer,
        "11/12/2026 In Wire - EXAMPLE CO $100.00")]
    [InlineData("cash-application-credit-plus-payment.json", 70.00, PaymentMethodType.WireTransfer,
        "11/12/2026 In Wire - EXAMPLE CO $70.00")]
    [InlineData("cash-application-dashboard-backdated-wire.json", 72.00, PaymentMethodType.WireTransfer,
        "06/03/2026 In Wire - EXAMPLE CO $72.00")]
    public async Task RecordAsync_CapturedStripePayment_CreatesCreditTransaction(
        string fixture, decimal expectedAmount, PaymentMethodType expectedMethod, string expectedDetails)
    {
        var (invoicePayment, paymentRecord) = await LoadFixtureAsync(fixture);
        SetupInvoicePayment(invoicePayment);
        _stripeFacade.GetPaymentRecord(paymentRecord.Id).Returns(paymentRecord);
        var expectedOrganizationId = Guid.Parse(invoicePayment.Invoice.Parent.SubscriptionDetails.Metadata["organizationId"]);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == expectedOrganizationId &&
            t.UserId == null &&
            t.ProviderId == null &&
            t.Type == TransactionType.Credit &&
            t.Gateway == GatewayType.Bank &&
            t.GatewayId == invoicePayment.Id &&
            t.PaymentMethodType == expectedMethod &&
            t.Amount == expectedAmount &&
            t.Details == expectedDetails &&
            t.CreationDate == invoicePayment.StatusTransitions.PaidAt));
    }

    [Fact]
    public async Task RecordAsync_DashboardEffectiveDate_UsedAsTransactionDate()
    {
        // Recorded on 2026-11-12 with Effective date June 3, 2026; Stripe stores it as paid_at, midnight US Eastern.
        var (invoicePayment, paymentRecord) = await LoadFixtureAsync("cash-application-dashboard-backdated-wire.json");
        SetupInvoicePayment(invoicePayment);
        _stripeFacade.GetPaymentRecord(paymentRecord.Id).Returns(paymentRecord);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.CreationDate == new DateTime(2026, 6, 3, 4, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task RecordAsync_NoPaidAt_UsesInvoicePaymentCreated()
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.StatusTransitions = null;
        SetupInvoicePayment(invoicePayment);
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.CreationDate == invoicePayment.Created));
    }

    [Fact]
    public async Task RecordAsync_ConcurrentDeliveryInsertedFirst_ReportsExistingWithoutError()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");
        _transactionRepository.GetByGatewayIdAsync(GatewayType.Bank, InvoicePaymentId)
            .Returns((Transaction)null, new Transaction { Amount = 100M, OrganizationId = _organizationId });
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns<Transaction>(_ => throw SqlExceptionFactory.Create(2601));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(2).GetByGatewayIdAsync(GatewayType.Bank, InvoicePaymentId);
        AssertNotLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_UniqueViolationButNoRowFound_Rethrows()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns<Transaction>(_ => throw SqlExceptionFactory.Create(2627));

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => _sut.RecordAsync(_invoicePayment));
    }

    [Fact]
    public async Task RecordAsync_OwnerDeleted_LogsErrorWithoutThrowing()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns<Transaction>(_ => throw SqlExceptionFactory.Create(547));

        await _sut.RecordAsync(_invoicePayment);

        AssertLogged(LogLevel.Error);
    }

    /// <summary>
    /// The invoice payment ID is the Transaction's GatewayId (50 characters); one that would not fit is logged and
    /// skipped rather than truncated, so it can never collide with another payment's ID.
    /// </summary>
    [Fact]
    public async Task RecordAsync_InvoicePaymentIdTooLongForGatewayId_LogsErrorAndCreatesNothing()
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.Id = "inpay_" + new string('x', 45);
        SetupInvoicePayment(invoicePayment);
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_OtherDatabaseError_Rethrows()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");
        _transactionRepository.CreateAsync(Arg.Any<Transaction>()).Returns<Transaction>(_ => throw SqlExceptionFactory.Create(1205));

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => _sut.RecordAsync(_invoicePayment));
    }

    [Theory]
    [InlineData("CHECK", PaymentMethodType.Check)]
    [InlineData("Cheque", PaymentMethodType.Check)]
    [InlineData("Wire", PaymentMethodType.WireTransfer)]
    [InlineData("WIRE TRANSFER", PaymentMethodType.WireTransfer)]
    [InlineData("ACH", PaymentMethodType.BankAccount)]
    [InlineData("Cash", PaymentMethodType.BankAccount)]
    [InlineData("Checkout", PaymentMethodType.BankAccount)]
    [InlineData(null, PaymentMethodType.BankAccount)]
    public async Task RecordAsync_PaymentType_MapsToMethod(string? paymentType, PaymentMethodType expected)
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord(paymentType);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t => t.PaymentMethodType == expected));
    }

    /// <summary>
    /// The reference carries the date received and the paying entity ("&lt;date&gt; - &lt;payer&gt;"); the payment
    /// type and amount come from their own fields, as Billing Operations enters them manually today.
    /// </summary>
    [Theory]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $100.00")]
    [InlineData("CHECK", "11/12/2026 - Example Corp #1001", "Acme Corp",
        "11/12/2026 Check In - Example Corp #1001 $100.00")]
    [InlineData("ACH", "11/10/2026 - Acme Holdings", "Acme Corp", "11/10/2026 ACH - Acme Holdings $100.00")]
    [InlineData(null, "11/10/2026 - Acme Holdings", "Acme Corp", "11/10/2026 Bank Payment - Acme Holdings $100.00")]
    [InlineData("Wire", "  11/10/2026  -  EXAMPLE PAYER LTD  ", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $100.00")]
    [InlineData("Wire", "11/10/2026 - Partner - EMEA", "Acme Corp", "11/10/2026 In Wire - Partner - EMEA $100.00")]
    [InlineData("Wire", "EXAMPLE PAYER LTD", "Acme Corp", "In Wire - EXAMPLE PAYER LTD $100.00")]
    [InlineData("CHECK", "1001", "Acme Corp", "Check In - Acme Corp #1001 $100.00")]
    [InlineData("CHECK", "#1001", "Acme Corp", "Check In - Acme Corp #1001 $100.00")]
    [InlineData("CHECK", "1001", null, "Check In - #1001 $100.00")]
    [InlineData("CHECK", null, "Acme Corp", "Check In - Acme Corp $100.00")]
    [InlineData("Wire", null, "Acme Corp", "In Wire - Acme Corp $100.00")]
    [InlineData("Wire", "   ", "Acme Corp", "In Wire - Acme Corp $100.00")]
    [InlineData(null, null, "Acme Corp", "Bank Payment - Acme Corp $100.00")]
    [InlineData("Wire", null, null, "In Wire $100.00")]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD $100.00", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $100.00")]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD $100", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $100.00")]
    [InlineData("CHECK", "11/12/2026 - Example Corp #1001 $100.00", "Acme Corp",
        "11/12/2026 Check In - Example Corp #1001 $100.00")]
    [InlineData("CHECK", "1001 $100.00", "Acme Corp", "Check In - Acme Corp #1001 $100.00")]
    [InlineData("Wire", "11/10/2026 - $100.00", "Acme Corp", "11/10/2026 In Wire - Acme Corp $100.00")]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD $250.00", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $100.00 of $250.00")]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD $5000", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $100.00 of $5,000.00")]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD $ USD", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD $ USD $100.00")]
    [InlineData("Wire", "11/10/2026 - EXAMPLE PAYER LTD US$100.00", "Acme Corp",
        "11/10/2026 In Wire - EXAMPLE PAYER LTD US$100.00 $100.00")]
    public async Task RecordAsync_Details_MatchManualStyle(
        string? paymentType, string? reference, string? customerName, string expected)
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord(paymentType, reference, customerName);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t => t.Details == expected));
    }

    [Fact]
    public async Task RecordAsync_ReferenceWithThousandsAmount_DropsMatchingAmount()
    {
        SetupInvoicePayment(CreateInvoicePayment(150000));
        SetupPaymentRecord("Wire", "11/10/2026 - EXAMPLE PAYER LTD $1,500.00");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.Details == "11/10/2026 In Wire - EXAMPLE PAYER LTD $1,500.00"));
    }

    [Fact]
    public async Task RecordAsync_LongPayerOnConsolidatedWire_ShortenedKeepingAmountAndTotal()
    {
        SetupInvoicePayment(CreateInvoicePayment(120000));
        SetupPaymentRecord("Wire", "11/10/2026 - " + new string('X', 150) + " $5,000.00");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.Details!.Length == 100 &&
            t.Details.StartsWith("11/10/2026 In Wire - XXX") &&
            t.Details.EndsWith("XXX $1,200.00 of $5,000.00")));
    }

    [Fact]
    public async Task RecordAsync_LongPayer_ShortenedKeepingDateTypeAndAmountAndLogged()
    {
        SetupInvoicePayment(CreateInvoicePayment(5380492));
        SetupPaymentRecord("Wire", "11/10/2026 - " + new string('X', 150));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.Details!.Length == 100 &&
            t.Details.StartsWith("11/10/2026 In Wire - XXX") &&
            t.Details.EndsWith("XXX $53,804.92")));
        AssertLogged(LogLevel.Warning);
    }

    [Theory]
    [InlineData("organizationId")]
    [InlineData("userId")]
    [InlineData("providerId")]
    public async Task RecordAsync_OwnerFromInvoiceSubscriptionMetadata(string ownerKey)
    {
        var ownerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000, new Dictionary<string, string> { [ownerKey] = ownerId.ToString() }));
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == (ownerKey == "organizationId" ? ownerId : null) &&
            t.UserId == (ownerKey == "userId" ? ownerId : null) &&
            t.ProviderId == (ownerKey == "providerId" ? ownerId : null)));
    }

    [Fact]
    public async Task RecordAsync_InvoiceMetadataOwner_WinsWithoutReadingCustomerSubscriptions()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(CreateSubscription("sub_other", "active", Day(1),
            metadata: new Dictionary<string, string> { ["userId"] = Guid.NewGuid().ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == _organizationId && t.UserId == null && t.ProviderId == null));
        await _stripeFacade.DidNotReceiveWithAnyArgs().ListSubscriptions(default);
        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(default);
    }

    /// <summary>
    /// Invoice metadata that does not identify exactly one valid owner falls back to the customer's current
    /// (not canceled) subscription, even when a canceled subscription ended more recently.
    /// </summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("unrelated keys only")]
    [InlineData("no subscription (standalone invoice)")]
    [InlineData("malformed id")]
    [InlineData("every owner ID malformed")]
    public async Task RecordAsync_NoUsableInvoiceOwner_UsesCustomersCurrentSubscription(string invoiceMetadata)
    {
        var invoicePayment = CreateInvoicePayment(10000, invoiceMetadata switch
        {
            "empty" => new Dictionary<string, string>(),
            "unrelated keys only" => new Dictionary<string, string> { ["trialInitiationPath"] = "product" },
            "malformed id" => new Dictionary<string, string> { ["organizationId"] = "not-a-guid" },
            "every owner ID malformed" => new Dictionary<string, string>
            {
                ["organizationId"] = "not-a-guid",
                ["userId"] = "also-not-a-guid",
                ["providerId"] = "x"
            },
            _ => null
        });
        if (invoiceMetadata == "no subscription (standalone invoice)")
        {
            invoicePayment.Invoice.Parent = null;
        }

        SetupInvoicePayment(invoicePayment);
        SetupPaymentRecord("WIRE");
        var currentOwnerId = Guid.NewGuid();
        SetupCustomerSubscriptions(
            CreateSubscription("sub_canceled", "canceled", Day(5), endedAt: Day(20),
                metadata: new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() }),
            // A blank userId (left by a premium user upgrading to an organization) counts as absent.
            CreateSubscription("sub_current", "past_due", Day(1), metadata: new Dictionary<string, string>
            {
                ["organizationId"] = currentOwnerId.ToString(),
                ["userId"] = string.Empty
            }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == currentOwnerId && t.UserId == null && t.ProviderId == null));
        AssertNotLogged(LogLevel.Error);
    }

    [Theory]
    [InlineData("organizationId")]
    [InlineData("userId")]
    [InlineData("providerId")]
    public async Task RecordAsync_NoInvoiceOwner_UsesOwnerFromCustomerSubscription(string ownerKey)
    {
        var ownerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000, subscriptionMetadata: []));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(CreateSubscription("sub_current", "active", Day(1),
            metadata: new Dictionary<string, string> { [ownerKey] = ownerId.ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == (ownerKey == "organizationId" ? ownerId : null) &&
            t.UserId == (ownerKey == "userId" ? ownerId : null) &&
            t.ProviderId == (ownerKey == "providerId" ? ownerId : null)));
    }

    /// <summary>
    /// As for card charges: the customer's subscriptions in Stripe's order, skipping canceled ones and ones without an
    /// owner; the first with an owner wins.
    /// </summary>
    [Fact]
    public async Task RecordAsync_CustomerSubscriptions_FirstNotCanceledWithOwnerWins()
    {
        var ownerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000, subscriptionMetadata: []));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(
            CreateSubscription("sub_canceled", "canceled", Day(1),
                metadata: new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() }),
            CreateSubscription("sub_expired", "incomplete_expired", Day(2),
                metadata: new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() }),
            CreateSubscription("sub_no_owner", "incomplete", Day(3),
                metadata: new Dictionary<string, string> { ["trialInitiationPath"] = "product" }),
            CreateSubscription("sub_owner", "active", Day(4),
                metadata: new Dictionary<string, string> { ["organizationId"] = ownerId.ToString() }),
            CreateSubscription("sub_later", "active", Day(5),
                metadata: new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t => t.OrganizationId == ownerId));
    }

    [Fact]
    public async Task RecordAsync_OnlyCanceledCustomerSubscriptions_NoTransactionWithWarning()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000, subscriptionMetadata: []));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(CreateSubscription("sub_canceled", "canceled", Day(1),
            metadata: new Dictionary<string, string> { ["userId"] = Guid.NewGuid().ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        AssertLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    /// <summary>
    /// Without an owner in the invoice's metadata copy, the invoice's own subscription comes before the customer's
    /// other subscriptions, even if it has since been canceled.
    /// </summary>
    [Fact]
    public async Task RecordAsync_InvoiceOwnSubscription_UsedBeforeCustomerSubscriptionsEvenIfCanceled()
    {
        var invoiceSubscriptionOwnerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000, subscriptionMetadata: []));
        SetupPaymentRecord("WIRE");
        _stripeFacade.GetSubscription(SubscriptionId).Returns(CreateSubscription(SubscriptionId, "canceled", Day(1),
            metadata: new Dictionary<string, string> { ["userId"] = invoiceSubscriptionOwnerId.ToString() }));
        SetupCustomerSubscriptions(CreateSubscription("sub_other", "active", Day(2),
            metadata: new Dictionary<string, string> { ["organizationId"] = Guid.NewGuid().ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.UserId == invoiceSubscriptionOwnerId && t.OrganizationId == null && t.ProviderId == null));
        await _stripeFacade.DidNotReceiveWithAnyArgs().ListSubscriptions(default);
    }

    [Fact]
    public async Task RecordAsync_InvoiceOwnSubscriptionWithoutOwner_FallsThroughToCustomerSubscriptions()
    {
        var customerOwnerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000, subscriptionMetadata: []));
        SetupPaymentRecord("WIRE");
        _stripeFacade.GetSubscription(SubscriptionId).Returns(CreateSubscription(SubscriptionId, "active", Day(1),
            metadata: new Dictionary<string, string> { ["trialInitiationPath"] = "product" }));
        SetupCustomerSubscriptions(CreateSubscription("sub_other", "active", Day(2),
            metadata: new Dictionary<string, string> { ["organizationId"] = customerOwnerId.ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t => t.OrganizationId == customerOwnerId));
    }

    /// <summary>
    /// Billing appends text to a metadata ID on purpose so other webhook handlers leave the owner alone; recording a
    /// payment still uses the ID at the start of the value. Surrounding whitespace is ignored.
    /// </summary>
    [Theory]
    [InlineData("organizationId", "{0}116757")]
    [InlineData("organizationId", "{0}-disabled")]
    [InlineData("organizationId", " {0}")]
    [InlineData("organizationId", " {0}116757 ")]
    [InlineData("userId", "{0}116757")]
    [InlineData("userId", "  {0}")]
    [InlineData("providerId", "{0}116757")]
    [InlineData("organizationId", "{0}")]
    public async Task RecordAsync_OwnerIdWithAppendedTextOrSpaces_UsesTheId(string ownerKey, string format)
    {
        var ownerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000,
            new Dictionary<string, string> { [ownerKey] = string.Format(CultureInfo.InvariantCulture, format, ownerId) }));
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == (ownerKey == "organizationId" ? ownerId : null) &&
            t.UserId == (ownerKey == "userId" ? ownerId : null) &&
            t.ProviderId == (ownerKey == "providerId" ? ownerId : null)));
        await _stripeFacade.DidNotReceiveWithAnyArgs().ListSubscriptions(default);
    }

    /// <summary>
    /// Text before the ID, or an ID cut short, is not an ID: the invoice metadata is unusable and the customer's
    /// subscription is used instead.
    /// </summary>
    [Theory]
    [InlineData("x{0}")]
    [InlineData("506d2-2b1b-47b1-ac45-a8a900f07e3c116757")]
    public async Task RecordAsync_OwnerIdNotAtStartOfValue_FallsBackToCustomerSubscription(string format)
    {
        var subscriptionOwnerId = Guid.NewGuid();
        SetupInvoicePayment(CreateInvoicePayment(10000,
            new Dictionary<string, string> { ["organizationId"] = string.Format(CultureInfo.InvariantCulture, format, Guid.NewGuid()) }));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(CreateSubscription("sub_current", "active", Day(1),
            metadata: new Dictionary<string, string> { ["organizationId"] = subscriptionOwnerId.ToString() }));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t => t.OrganizationId == subscriptionOwnerId));
    }

    /// <summary>
    /// The exact value form Billing uses: a complete ID with digits appended to keep other webhooks away.
    /// </summary>
    [Fact]
    public async Task RecordAsync_BillingExampleWithAppendedDigits_MapsToTheId()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000,
            new Dictionary<string, string> { ["organizationId"] = "835506d2-2b1b-47b1-ac45-a8a900f07e3c116757" }));
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == Guid.Parse("835506d2-2b1b-47b1-ac45-a8a900f07e3c") &&
            t.UserId == null && t.ProviderId == null));
    }

    /// <summary>
    /// The record Stripe generates when an invoice is paid out of band with no payment details (how Bitwarden's PayPal
    /// flow settles invoices) is not a cash application payment and is never posted. Captured from Stripe test mode.
    /// </summary>
    [Fact]
    public async Task RecordAsync_StripeGeneratedOutOfBandRecord_Skipped()
    {
        var (invoicePayment, paymentRecord) = await LoadFixtureAsync("cash-application-api-paid-out-of-band.json");
        SetupInvoicePayment(invoicePayment);
        _stripeFacade.GetPaymentRecord(paymentRecord.Id).Returns(paymentRecord);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        AssertNotLogged(LogLevel.Error);
    }

    /// <summary>
    /// A PayPal attempt can leave Braintree metadata on an invoice; a real cash application payment applied to it later
    /// is still posted, because only the record itself decides.
    /// </summary>
    [Fact]
    public async Task RecordAsync_InvoiceWithBraintreeMetadataAndCashApplicationRecord_Posted()
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.Invoice.Metadata = new Dictionary<string, string> { ["btTransactionId"] = "bt_123" };
        SetupInvoicePayment(invoicePayment);
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.Amount == 100M && t.PaymentMethodType == PaymentMethodType.WireTransfer));
    }

    /// <summary>
    /// A fee (e.g. a wire fee the bank deducted) is applied to settle the invoice but is not money received.
    /// </summary>
    [Theory]
    [InlineData("Wire Fee")]
    [InlineData("Bank Fee")]
    [InlineData("FEE")]
    [InlineData("wire-fee")]
    [InlineData("Fee - wire")]
    public async Task RecordAsync_FeePaymentType_Skipped(string paymentType)
    {
        SetupInvoicePayment(CreateInvoicePayment(2500));
        SetupPaymentRecord(paymentType);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        AssertNotLogged(LogLevel.Error);
    }

    /// <summary>
    /// FEE must start or end the payment type; a name that merely contains the word is a real payment.
    /// </summary>
    [Fact]
    public async Task RecordAsync_PaymentTypeContainingFeeInsideName_Posted()
    {
        SetupInvoicePayment(CreateInvoicePayment(1500000));
        SetupPaymentRecord("Wire - Fee Associates LLC");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.Amount == 15000M && t.PaymentMethodType == PaymentMethodType.WireTransfer));
    }

    /// <summary>
    /// Shortening a long customer name never splits a character made of two UTF-16 units, such as an emoji.
    /// </summary>
    [Fact]
    public async Task RecordAsync_LongCustomerNameWithEmojiAtCut_NotSplit()
    {
        // The prefix may be 92 characters (100 minus " $100.00"): "In Wire - " (10) + 81 letters puts the emoji's
        // first half at the last allowed position.
        var customerName = new string('A', 81) + "\U0001F600" + new string('B', 20);
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE", customerName: customerName);

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.Details!.Length <= 100 &&
            !t.Details.Any(char.IsSurrogate) &&
            t.Details.EndsWith(" $100.00")));
    }

    /// <summary>
    /// Reseller providers are listed for statements but are never the billing entity when an organization exists: with
    /// both present the organization is the owner, even if the providerId value is not a valid ID.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task RecordAsync_OrganizationAndProvider_OrganizationIsTheOwner(bool onInvoice, bool malformedProviderId)
    {
        var organizationId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["organizationId"] = organizationId.ToString(),
            ["providerId"] = malformedProviderId ? "not-a-guid" : Guid.NewGuid().ToString()
        };
        SetupInvoicePayment(CreateInvoicePayment(10000, onInvoice ? metadata : []));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(CreateSubscription("sub_current", "active", Day(1), metadata: metadata));

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == organizationId && t.UserId == null && t.ProviderId == null));
        AssertNotLogged(LogLevel.Warning);
    }

    /// <summary>
    /// Owners never conflict: the first valid ID by precedence organizationId, then userId, then providerId is the owner,
    /// on the invoice and on the subscription alike. A malformed ID is skipped in favor of the next valid one.
    /// </summary>
    [Theory]
    [InlineData("organizationId,userId", "organizationId", true)]
    [InlineData("organizationId,userId", "organizationId", false)]
    [InlineData("userId,providerId", "userId", true)]
    [InlineData("userId,providerId", "userId", false)]
    [InlineData("organizationId,userId,providerId", "organizationId", true)]
    [InlineData("organizationId,userId,providerId", "organizationId", false)]
    [InlineData("organizationId!,userId", "userId", true)]
    [InlineData("organizationId!,userId!,providerId", "providerId", false)]
    public async Task RecordAsync_SeveralOwnerIds_PrecedenceOrganizationThenUserThenProvider(
        string keys, string expectedOwnerKey, bool onInvoice)
    {
        var ids = new Dictionary<string, Guid>();
        var metadata = new Dictionary<string, string>();
        foreach (var entry in keys.Split(','))
        {
            var key = entry.TrimEnd('!');
            ids[key] = Guid.NewGuid();
            metadata[key] = entry.EndsWith('!') ? "not-a-guid" : ids[key].ToString();
        }

        SetupInvoicePayment(CreateInvoicePayment(10000, onInvoice ? metadata : []));
        SetupPaymentRecord("WIRE");
        SetupCustomerSubscriptions(CreateSubscription("sub_current", "active", Day(1), metadata: metadata));

        await _sut.RecordAsync(_invoicePayment);

        var expected = ids[expectedOwnerKey];
        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t.OrganizationId == (expectedOwnerKey == "organizationId" ? expected : null) &&
            t.UserId == (expectedOwnerKey == "userId" ? expected : null) &&
            t.ProviderId == (expectedOwnerKey == "providerId" ? expected : null)));
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_TransactionCreated_LogsAuditLine()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Any<Transaction>());
        _loggerFactory.Received(1).CreateLogger("Bit.Billing.NonChargeCashApplicationAudit");
        _auditLogger.Received(1).Log(LogLevel.Information, Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.StartsWith("Posted non-charge cash application payment " + InvoicePaymentId)),
            Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
    }

    /// <summary>
    /// Subscriptions without owner metadata occur: nothing is written, a warning is logged and the event completes
    /// normally (no error, no exception, so Stripe does not retry).
    /// </summary>
    [Theory]
    [InlineData("no metadata")]
    [InlineData("unrelated keys only")]
    [InlineData("malformed id")]
    [InlineData("every owner ID malformed")]
    [InlineData("no subscriptions")]
    public async Task RecordAsync_NoOwnerOnInvoiceOrSubscription_SkipsWithWarning(string subscriptionMetadata)
    {
        SetupInvoicePayment(CreateInvoicePayment(10000, subscriptionMetadata: []));
        SetupPaymentRecord("WIRE");
        if (subscriptionMetadata == "no subscriptions")
        {
            SetupCustomerSubscriptions();
        }
        else
        {
            SetupCustomerSubscriptions(CreateSubscription("sub_current", "active", Day(1), metadata: subscriptionMetadata switch
            {
                "unrelated keys only" => new Dictionary<string, string> { ["trialInitiationPath"] = "product" },
                "malformed id" => new Dictionary<string, string> { ["userId"] = "not-a-guid" },
                "every owner ID malformed" => new Dictionary<string, string>
                {
                    ["organizationId"] = "not-a-guid",
                    ["userId"] = "also-not-a-guid"
                },
                _ => null
            }));
        }

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        AssertLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_InvoiceWithoutCustomer_SkipsWithWarning()
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.Invoice.Parent = null;
        invoicePayment.Invoice.CustomerId = null;
        SetupInvoicePayment(invoicePayment);
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await _stripeFacade.DidNotReceiveWithAnyArgs().ListSubscriptions(default);
        AssertLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_StripeChargePayment_Ignored()
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.Payment = new InvoicePaymentPayment { Type = "payment_intent", PaymentIntentId = "pi_123" };
        SetupInvoicePayment(invoicePayment);

        await _sut.RecordAsync(_invoicePayment);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentRecord(default!);
        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task RecordAsync_PaymentRecordProcessedByStripe_Ignored()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        _stripeFacade.GetPaymentRecord(PaymentRecordId).Returns(new PaymentRecord
        {
            Id = PaymentRecordId,
            PaymentMethodDetails = new PaymentRecordPaymentMethodDetails { Type = "card" },
            ProcessorDetails = new PaymentRecordProcessorDetails { Type = "stripe" }
        });

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task RecordAsync_CustomProcessor_IsNonChargeCashApplication()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        _stripeFacade.GetPaymentRecord(PaymentRecordId).Returns(new PaymentRecord
        {
            Id = PaymentRecordId,
            PaymentMethodDetails = new PaymentRecordPaymentMethodDetails { Type = "us_bank_account" },
            ProcessorDetails = new PaymentRecordProcessorDetails { Type = "custom" }
        });

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.Received(1).CreateAsync(Arg.Any<Transaction>());
    }

    [Theory]
    [InlineData("open", 10000L)]
    [InlineData("paid", 0L)]
    [InlineData("paid", null)]
    public async Task RecordAsync_NotPaidOrNoAmount_Ignored(string status, long? amountPaid)
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.Status = status;
        invoicePayment.AmountPaid = amountPaid;
        SetupInvoicePayment(invoicePayment);

        await _sut.RecordAsync(_invoicePayment);

        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentRecord(default!);
        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task RecordAsync_NonUsdCurrency_LogsErrorAndCreatesNothing()
    {
        var invoicePayment = CreateInvoicePayment(10000);
        invoicePayment.Currency = "eur";
        SetupInvoicePayment(invoicePayment);
        SetupPaymentRecord("WIRE");

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_AlreadyRecorded_CreatesNothing()
    {
        SetupInvoicePayment(CreateInvoicePayment(10000));
        SetupPaymentRecord("WIRE");
        _transactionRepository.GetByGatewayIdAsync(GatewayType.Bank, InvoicePaymentId)
            .Returns(new Transaction { Amount = 100M, OrganizationId = _organizationId });

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await _stripeFacade.DidNotReceiveWithAnyArgs().GetPaymentRecord(default!);
        await _stripeFacade.DidNotReceiveWithAnyArgs().ListSubscriptions(default);
        AssertNotLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task RecordAsync_AlreadyRecordedAmountEditedInAdmin_LogsWarningAndLeavesIt()
    {
        SetupInvoicePayment(CreateInvoicePayment(100000));
        SetupPaymentRecord("CHECK");
        _transactionRepository.GetByGatewayIdAsync(GatewayType.Bank, InvoicePaymentId)
            .Returns(new Transaction { Amount = 1200M, OrganizationId = _organizationId });

        await _sut.RecordAsync(_invoicePayment);

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await _transactionRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        AssertLogged(LogLevel.Warning);
        AssertNotLogged(LogLevel.Error);
    }

    private static async Task<(InvoicePayment, PaymentRecord)> LoadFixtureAsync(string fileName)
    {
        using var document = JsonDocument.Parse(await EmbeddedResourceReader.ReadAsync("Stripe", fileName));
        var root = document.RootElement;
        return (StripeEntity.FromJson<InvoicePayment>(root.GetProperty("invoice_payment").GetRawText()),
            StripeEntity.FromJson<PaymentRecord>(root.GetProperty("payment_record").GetRawText()));
    }

    private InvoicePayment CreateInvoicePayment(long amountPaid, Dictionary<string, string> subscriptionMetadata = null) => new()
    {
        Id = InvoicePaymentId,
        Status = "paid",
        Currency = "usd",
        AmountPaid = amountPaid,
        AmountRequested = amountPaid,
        Created = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
        StatusTransitions = new InvoicePaymentStatusTransitions { PaidAt = _paidAt },
        Payment = new InvoicePaymentPayment { Type = "payment_record", PaymentRecordId = PaymentRecordId },
        InvoiceId = InvoiceId,
        Invoice = new Invoice
        {
            Id = InvoiceId,
            CustomerId = CustomerId,
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails
                {
                    SubscriptionId = SubscriptionId,
                    Metadata = subscriptionMetadata ??
                               new Dictionary<string, string> { ["organizationId"] = _organizationId.ToString() }
                }
            }
        }
    };

    private void SetupInvoicePayment(InvoicePayment invoicePayment) => _invoicePayment = invoicePayment;

    private void SetupCustomerSubscriptions(params Subscription[] subscriptions) =>
        _stripeFacade.ListSubscriptions(Arg.Is<SubscriptionListOptions>(o => o.Customer == CustomerId && o.Status == null))
            .Returns(new StripeList<Subscription> { Data = subscriptions.ToList() });

    private static Subscription CreateSubscription(string id, string status, DateTime created, DateTime? endedAt = null,
        Dictionary<string, string> metadata = null) => new()
        {
            Id = id,
            Status = status,
            Created = created,
            EndedAt = endedAt,
            Metadata = metadata
        };

    private static DateTime Day(int day) => new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);

    private void SetupPaymentRecord(string? paymentType, string? reference = null, string? customerName = "Acme Corp") =>
        _stripeFacade.GetPaymentRecord(PaymentRecordId).Returns(new PaymentRecord
        {
            Id = PaymentRecordId,
            Description = "Payment for invoice",
            CustomerDetails = new PaymentRecordCustomerDetails { Name = customerName },
            PaymentMethodDetails = new PaymentRecordPaymentMethodDetails
            {
                Type = "custom",
                Custom = new PaymentRecordPaymentMethodDetailsCustom { DisplayName = paymentType, Type = "custom" }
            },
            ProcessorDetails = new PaymentRecordProcessorDetails
            {
                Type = "custom",
                Custom = new PaymentRecordProcessorDetailsCustom { PaymentReference = reference }
            }
        });

    private void AssertLogged(LogLevel level) =>
        _logger.Received().Log(level, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());

    private void AssertNotLogged(LogLevel level) =>
        _logger.DidNotReceive().Log(level, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
}
