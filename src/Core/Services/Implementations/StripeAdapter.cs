using Bit.Core.Billing.Extensions;
using Bit.Core.Models.BitStripe;
using Stripe;

using TestClockService = Stripe.TestHelpers.TestClockService;

namespace Bit.Core.Services.Implementations;

public class StripeAdapter : IStripeAdapter
{
    private readonly CustomerService _customerService = new();
    private readonly SubscriptionService _subscriptionService = new();
    private readonly InvoiceService _invoiceService = new();
    private readonly PaymentMethodService _paymentMethodService = new();
    private readonly TaxRateService _taxRateService = new();
    private readonly TaxIdService _taxIdService = new();
    private readonly ChargeService _chargeService = new();
    private readonly RefundService _refundService = new();
    private readonly CardService _cardService = new();
    private readonly BankAccountService _bankAccountService = new();
    private readonly PriceService _priceService = new();
    private readonly SetupIntentService _setupIntentService = new();
    private readonly TestClockService _testClockService = new();

    #nullable enable

    #region Customers

    public Task<Customer> CustomerCreate(CustomerCreateOptions options)
        => _customerService.CreateAsync(options);

    public Task<Customer> CustomerDelete(string id)
        => _customerService.DeleteAsync(id);

    public Task<Customer> CustomerGet(string id, CustomerGetOptions? options = null)
        => _customerService.GetAsync(id, options);

    public async Task<List<PaymentMethod>> CustomerListPaymentMethods(
        string id,
        CustomerListPaymentMethodsOptions? options = null)
    {
        var paymentMethods = await _customerService.ListPaymentMethodsAsync(id, options);
        return paymentMethods.Data;
    }

    public async Task<Customer?> CustomerTryGet(string id, CustomerGetOptions? options = null)
    {
        try
        {
            return await _customerService.GetAsync(id, options);
        }
        catch (StripeException exception) when (exception.ResourceMissing())
        {
            return null;
        }
    }

    public Task<Customer> CustomerUpdate(string id, CustomerUpdateOptions? options = null)
        => _customerService.UpdateAsync(id, options);

    #endregion

    #region Subscriptions

    public Task<Subscription> SubscriptionCancel(string id, SubscriptionCancelOptions? options = null)
        => _subscriptionService.CancelAsync(id, options);

    public Task<Subscription> SubscriptionCreate(SubscriptionCreateOptions options)
        => _subscriptionService.CreateAsync(options);

    public Task<Subscription> SubscriptionGet(string id, SubscriptionGetOptions? options = null)
        => _subscriptionService.GetAsync(id, options);

    public async Task<List<Subscription>> SubscriptionList(StripeSubscriptionListOptions options)
    {
        if (!options.SelectAll)
        {
            return (await _subscriptionService.ListAsync(options.ToStripeApiOptions())).Data;
        }

        options.Limit = 100;
        var items = new List<Subscription>();
        await foreach (var i in _subscriptionService.ListAutoPagingAsync(options.ToStripeApiOptions()))
        {
            items.Add(i);
        }
        return items;
    }

    public async Task<Subscription?> SubscriptionTryGet(string id, SubscriptionGetOptions? options = null)
    {
        try
        {
            return await _subscriptionService.GetAsync(id, options);
        }
        catch (StripeException exception) when (exception.ResourceMissing())
        {
            return null;
        }
    }

    public Task<Subscription> SubscriptionUpdate(string id, SubscriptionUpdateOptions? options = null)
        => _subscriptionService.UpdateAsync(id, options);

    #endregion

    #nullable disable

    public Task<Stripe.Invoice> InvoiceUpcomingAsync(Stripe.UpcomingInvoiceOptions options)
    {
        return _invoiceService.UpcomingAsync(options);
    }

    public Task<Stripe.Invoice> InvoiceGetAsync(string id, Stripe.InvoiceGetOptions options)
    {
        return _invoiceService.GetAsync(id, options);
    }

    public async Task<List<Stripe.Invoice>> InvoiceListAsync(StripeInvoiceListOptions options)
    {
        if (!options.SelectAll)
        {
            return (await _invoiceService.ListAsync(options.ToInvoiceListOptions())).Data;
        }

        options.Limit = 100;

        var invoices = new List<Stripe.Invoice>();

        await foreach (var invoice in _invoiceService.ListAutoPagingAsync(options.ToInvoiceListOptions()))
        {
            invoices.Add(invoice);
        }

        return invoices;
    }

    public async Task<List<Stripe.Invoice>> InvoiceSearchAsync(InvoiceSearchOptions options)
        => (await _invoiceService.SearchAsync(options)).Data;

    public Task<Stripe.Invoice> InvoiceUpdateAsync(string id, Stripe.InvoiceUpdateOptions options)
    {
        return _invoiceService.UpdateAsync(id, options);
    }

    public Task<Stripe.Invoice> InvoiceFinalizeInvoiceAsync(string id, Stripe.InvoiceFinalizeOptions options)
    {
        return _invoiceService.FinalizeInvoiceAsync(id, options);
    }

    public Task<Stripe.Invoice> InvoiceSendInvoiceAsync(string id, Stripe.InvoiceSendOptions options)
    {
        return _invoiceService.SendInvoiceAsync(id, options);
    }

    public Task<Stripe.Invoice> InvoicePayAsync(string id, Stripe.InvoicePayOptions options = null)
    {
        return _invoiceService.PayAsync(id, options);
    }

    public Task<Stripe.Invoice> InvoiceDeleteAsync(string id, Stripe.InvoiceDeleteOptions options = null)
    {
        return _invoiceService.DeleteAsync(id, options);
    }

    public Task<Stripe.Invoice> InvoiceVoidInvoiceAsync(string id, Stripe.InvoiceVoidOptions options = null)
    {
        return _invoiceService.VoidInvoiceAsync(id, options);
    }

    public IEnumerable<Stripe.PaymentMethod> PaymentMethodListAutoPaging(Stripe.PaymentMethodListOptions options)
    {
        return _paymentMethodService.ListAutoPaging(options);
    }

    public IAsyncEnumerable<Stripe.PaymentMethod> PaymentMethodListAutoPagingAsync(Stripe.PaymentMethodListOptions options)
        => _paymentMethodService.ListAutoPagingAsync(options);

    public Task<Stripe.PaymentMethod> PaymentMethodAttachAsync(string id, Stripe.PaymentMethodAttachOptions options = null)
    {
        return _paymentMethodService.AttachAsync(id, options);
    }

    public Task<Stripe.PaymentMethod> PaymentMethodDetachAsync(string id, Stripe.PaymentMethodDetachOptions options = null)
    {
        return _paymentMethodService.DetachAsync(id, options);
    }

    public Task<Stripe.TaxRate> TaxRateCreateAsync(Stripe.TaxRateCreateOptions options)
    {
        return _taxRateService.CreateAsync(options);
    }

    public Task<Stripe.TaxRate> TaxRateUpdateAsync(string id, Stripe.TaxRateUpdateOptions options)
    {
        return _taxRateService.UpdateAsync(id, options);
    }

    public Task<Stripe.TaxId> TaxIdCreateAsync(string id, Stripe.TaxIdCreateOptions options)
    {
        return _taxIdService.CreateAsync(id, options);
    }

    public Task<Stripe.TaxId> TaxIdDeleteAsync(string customerId, string taxIdId,
        Stripe.TaxIdDeleteOptions options = null)
    {
        return _taxIdService.DeleteAsync(customerId, taxIdId);
    }

    public Task<Stripe.StripeList<Stripe.Charge>> ChargeListAsync(Stripe.ChargeListOptions options)
    {
        return _chargeService.ListAsync(options);
    }

    public Task<Stripe.Refund> RefundCreateAsync(Stripe.RefundCreateOptions options)
    {
        return _refundService.CreateAsync(options);
    }

    public Task<Stripe.Card> CardDeleteAsync(string customerId, string cardId, Stripe.CardDeleteOptions options = null)
    {
        return _cardService.DeleteAsync(customerId, cardId, options);
    }

    public Task<Stripe.BankAccount> BankAccountCreateAsync(string customerId, Stripe.BankAccountCreateOptions options = null)
    {
        return _bankAccountService.CreateAsync(customerId, options);
    }

    public Task<Stripe.BankAccount> BankAccountDeleteAsync(string customerId, string bankAccount, Stripe.BankAccountDeleteOptions options = null)
    {
        return _bankAccountService.DeleteAsync(customerId, bankAccount, options);
    }

    public async Task<Stripe.StripeList<Stripe.Price>> PriceListAsync(Stripe.PriceListOptions options = null)
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
}
