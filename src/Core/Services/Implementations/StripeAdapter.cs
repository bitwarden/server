// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;
using Stripe.Tax;

namespace Bit.Core.Services;

public class StripeAdapter : IStripeAdapter
{
    private readonly CustomerService _customerService;
    private readonly SubscriptionService _subscriptionService;
    private readonly InvoiceService _invoiceService;
    private readonly PaymentMethodService _paymentMethodService;
    private readonly TaxIdService _taxIdService;
    private readonly ChargeService _chargeService;
    private readonly RefundService _refundService;
    private readonly CardService _cardService;
    private readonly BankAccountService _bankAccountService;
    private readonly PlanService _planService;
    private readonly PriceService _priceService;
    private readonly SetupIntentService _setupIntentService;
    private readonly Stripe.TestHelpers.TestClockService _testClockService;
    private readonly CustomerBalanceTransactionService _customerBalanceTransactionService;
    private readonly Stripe.Tax.RegistrationService _taxRegistrationService;
    private readonly CalculationService _calculationService;

    public StripeAdapter()
    {
        _customerService = new CustomerService();
        _subscriptionService = new SubscriptionService();
        _invoiceService = new InvoiceService();
        _paymentMethodService = new PaymentMethodService();
        _taxIdService = new TaxIdService();
        _chargeService = new ChargeService();
        _refundService = new RefundService();
        _cardService = new CardService();
        _bankAccountService = new BankAccountService();
        _priceService = new PriceService();
        _planService = new PlanService();
        _setupIntentService = new SetupIntentService();
        _testClockService = new Stripe.TestHelpers.TestClockService();
        _customerBalanceTransactionService = new CustomerBalanceTransactionService();
        _taxRegistrationService = new Stripe.Tax.RegistrationService();
        _calculationService = new CalculationService();
    }

    public Task<Customer> CustomerCreateAsync(CustomerCreateOptions options)
    {
        return _customerService.CreateAsync(options);
    }

    public Task CustomerDeleteDiscountAsync(string customerId, CustomerDeleteDiscountOptions options = null) =>
        _customerService.DeleteDiscountAsync(customerId, options);

    public Task<Customer> CustomerGetAsync(string id, CustomerGetOptions options = null)
    {
        return _customerService.GetAsync(id, options);
    }

    public Task<Customer> CustomerUpdateAsync(string id, CustomerUpdateOptions options = null)
    {
        return _customerService.UpdateAsync(id, options);
    }

    public Task<Customer> CustomerDeleteAsync(string id)
    {
        return _customerService.DeleteAsync(id);
    }

    public async Task<List<PaymentMethod>> CustomerListPaymentMethods(string id,
        CustomerPaymentMethodListOptions options = null)
    {
        var paymentMethods = await _customerService.ListPaymentMethodsAsync(id, options);
        return paymentMethods.Data;
    }

    public async Task<CustomerBalanceTransaction> CustomerBalanceTransactionCreate(string customerId,
        CustomerBalanceTransactionCreateOptions options)
        => await _customerBalanceTransactionService.CreateAsync(customerId, options);

    public Task<Subscription> SubscriptionCreateAsync(SubscriptionCreateOptions options)
    {
        return _subscriptionService.CreateAsync(options);
    }

    public Task<Subscription> SubscriptionGetAsync(string id, SubscriptionGetOptions options = null)
    {
        return _subscriptionService.GetAsync(id, options);
    }

    public async Task<Subscription> ProviderSubscriptionGetAsync(
        string id,
        Guid providerId,
        SubscriptionGetOptions options = null)
    {
        var subscription = await _subscriptionService.GetAsync(id, options);
        if (subscription.Metadata.TryGetValue("providerId", out var value) && value == providerId.ToString())
        {
            return subscription;
        }

        throw new InvalidOperationException("Subscription does not belong to the provider.");
    }

    public Task<Subscription> SubscriptionUpdateAsync(string id,
        SubscriptionUpdateOptions options = null)
    {
        return _subscriptionService.UpdateAsync(id, options);
    }

    public Task<Subscription> SubscriptionCancelAsync(string Id, SubscriptionCancelOptions options = null)
    {
        return _subscriptionService.CancelAsync(Id, options);
    }

    public Task<Invoice> InvoiceGetAsync(string id, InvoiceGetOptions options)
    {
        return _invoiceService.GetAsync(id, options);
    }

    public async Task<List<Invoice>> InvoiceListAsync(StripeInvoiceListOptions options)
    {
        if (!options.SelectAll)
        {
            return (await _invoiceService.ListAsync(options.ToInvoiceListOptions())).Data;
        }

        options.Limit = 100;

        var invoices = new List<Invoice>();

        await foreach (var invoice in _invoiceService.ListAutoPagingAsync(options.ToInvoiceListOptions()))
        {
            invoices.Add(invoice);
        }

        return invoices;
    }

    public Task<Invoice> InvoiceCreatePreviewAsync(InvoiceCreatePreviewOptions options)
    {
        return _invoiceService.CreatePreviewAsync(options);
    }

    public async Task<List<Invoice>> InvoiceSearchAsync(InvoiceSearchOptions options)
        => (await _invoiceService.SearchAsync(options)).Data;

    public Task<Invoice> InvoiceUpdateAsync(string id, InvoiceUpdateOptions options)
    {
        return _invoiceService.UpdateAsync(id, options);
    }

    public Task<Invoice> InvoiceFinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options)
    {
        return _invoiceService.FinalizeInvoiceAsync(id, options);
    }

    public Task<Invoice> InvoiceSendInvoiceAsync(string id, InvoiceSendOptions options)
    {
        return _invoiceService.SendInvoiceAsync(id, options);
    }

    public Task<Invoice> InvoicePayAsync(string id, InvoicePayOptions options = null)
    {
        return _invoiceService.PayAsync(id, options);
    }

    public Task<Invoice> InvoiceDeleteAsync(string id, InvoiceDeleteOptions options = null)
    {
        return _invoiceService.DeleteAsync(id, options);
    }

    public Task<Invoice> InvoiceVoidInvoiceAsync(string id, InvoiceVoidOptions options = null)
    {
        return _invoiceService.VoidInvoiceAsync(id, options);
    }

    public IEnumerable<PaymentMethod> PaymentMethodListAutoPaging(PaymentMethodListOptions options)
    {
        return _paymentMethodService.ListAutoPaging(options);
    }

    public IAsyncEnumerable<PaymentMethod> PaymentMethodListAutoPagingAsync(PaymentMethodListOptions options)
        => _paymentMethodService.ListAutoPagingAsync(options);

    public Task<PaymentMethod> PaymentMethodAttachAsync(string id, PaymentMethodAttachOptions options = null)
    {
        return _paymentMethodService.AttachAsync(id, options);
    }

    public Task<PaymentMethod> PaymentMethodDetachAsync(string id, PaymentMethodDetachOptions options = null)
    {
        return _paymentMethodService.DetachAsync(id, options);
    }

    public Task<Plan> PlanGetAsync(string id, PlanGetOptions options = null)
    {
        return _planService.GetAsync(id, options);
    }

    public Task<TaxId> TaxIdCreateAsync(string id, TaxIdCreateOptions options)
    {
        return _taxIdService.CreateAsync(id, options);
    }

    public Task<TaxId> TaxIdDeleteAsync(string customerId, string taxIdId,
        TaxIdDeleteOptions options = null)
    {
        return _taxIdService.DeleteAsync(customerId, taxIdId);
    }

    public Task<StripeList<Registration>> TaxRegistrationsListAsync(RegistrationListOptions options = null)
    {
        return _taxRegistrationService.ListAsync(options);
    }

    public Task<StripeList<Charge>> ChargeListAsync(ChargeListOptions options)
    {
        return _chargeService.ListAsync(options);
    }

    public Task<Refund> RefundCreateAsync(RefundCreateOptions options)
    {
        return _refundService.CreateAsync(options);
    }

    public Task<Card> CardDeleteAsync(string customerId, string cardId, CardDeleteOptions options = null)
    {
        return _cardService.DeleteAsync(customerId, cardId, options);
    }

    public Task<BankAccount> BankAccountCreateAsync(string customerId, BankAccountCreateOptions options = null)
    {
        return _bankAccountService.CreateAsync(customerId, options);
    }

    public Task<BankAccount> BankAccountDeleteAsync(string customerId, string bankAccount, BankAccountDeleteOptions options = null)
    {
        return _bankAccountService.DeleteAsync(customerId, bankAccount, options);
    }

    public async Task<StripeList<Price>> PriceListAsync(PriceListOptions options = null)
    {
        return await _priceService.ListAsync(options);
    }

    public Task<SetupIntent> SetupIntentCreate(SetupIntentCreateOptions options)
        => _setupIntentService.CreateAsync(options);

    public async Task<List<SetupIntent>> SetupIntentList(SetupIntentListOptions options)
    {
        var setupIntents = await _setupIntentService.ListAsync(options);

        return setupIntents.Data;
    }

    public Task SetupIntentCancel(string id, SetupIntentCancelOptions options = null)
        => _setupIntentService.CancelAsync(id, options);

    public Task<SetupIntent> SetupIntentGet(string id, SetupIntentGetOptions options = null)
        => _setupIntentService.GetAsync(id, options);

    public Task SetupIntentVerifyMicroDeposit(string id, SetupIntentVerifyMicrodepositsOptions options)
        => _setupIntentService.VerifyMicrodepositsAsync(id, options);

    public async Task<List<Stripe.TestHelpers.TestClock>> TestClockListAsync()
    {
        var items = new List<Stripe.TestHelpers.TestClock>();
        var options = new Stripe.TestHelpers.TestClockListOptions()
        {
            Limit = 100
        };
        await foreach (var i in _testClockService.ListAutoPagingAsync(options))
        {
            items.Add(i);
        }
        return items;
    }

    public Task<Price> PriceGetAsync(string id, PriceGetOptions options = null)
        => _priceService.GetAsync(id, options);
}
