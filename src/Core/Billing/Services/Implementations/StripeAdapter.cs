// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;
using Stripe.TestHelpers;
using CustomerService = Stripe.CustomerService;
using RefundService = Stripe.RefundService;

namespace Bit.Core.Billing.Services.Implementations;

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
    private readonly PriceService _priceService;
    private readonly SetupIntentService _setupIntentService;
    private readonly TestClockService _testClockService;
    private readonly CustomerBalanceTransactionService _customerBalanceTransactionService;

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
        _setupIntentService = new SetupIntentService();
        _testClockService = new TestClockService();
        _customerBalanceTransactionService = new CustomerBalanceTransactionService();
    }

    /**************
     ** CUSTOMER **
     **************/
    public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options)
    {
        return _customerService.CreateAsync(options);
    }

    public Task<Customer> GetCustomerAsync(string id, CustomerGetOptions options = null)
    {
        return _customerService.GetAsync(id, options);
    }

    public Task<Customer> UpdateCustomerAsync(string id, CustomerUpdateOptions options = null)
    {
        return _customerService.UpdateAsync(id, options);
    }

    public Task<Customer> DeleteCustomerAsync(string id)
    {
        return _customerService.DeleteAsync(id);
    }

    public async Task<List<PaymentMethod>> ListCustomerPaymentMethods(string id,
        CustomerListPaymentMethodsOptions options = null)
    {
        var paymentMethods = await _customerService.ListPaymentMethodsAsync(id, options);
        return paymentMethods.Data;
    }

    public async Task<CustomerBalanceTransaction> CreateCustomerBalanceTransactionAsync(string customerId,
        CustomerBalanceTransactionCreateOptions options)
    {
        return await _customerBalanceTransactionService.CreateAsync(customerId, options);
    }

    /******************
     ** SUBSCRIPTION **
     ******************/
    public Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions options)
    {
        return _subscriptionService.CreateAsync(options);
    }

    public Task<Subscription> GetSubscriptionAsync(string id, SubscriptionGetOptions options = null)
    {
        return _subscriptionService.GetAsync(id, options);
    }

    public async Task<Subscription> GetProviderSubscriptionAsync(
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

    public Task<Subscription> UpdateSubscriptionAsync(string id,
        SubscriptionUpdateOptions options = null)
    {
        return _subscriptionService.UpdateAsync(id, options);
    }

    public Task<Subscription> CancelSubscriptionAsync(string id, SubscriptionCancelOptions options = null)
    {
        return _subscriptionService.CancelAsync(id, options);
    }

    public async Task<List<Subscription>> ListSubscriptionsAsync(StripeSubscriptionListOptions options)
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

    /*************
     ** INVOICE **
     *************/
    public Task<Invoice> GetUpcomingInvoiceAsync(UpcomingInvoiceOptions options)
    {
        return _invoiceService.UpcomingAsync(options);
    }

    public Task<Invoice> GetInvoiceAsync(string id, InvoiceGetOptions options)
    {
        return _invoiceService.GetAsync(id, options);
    }

    public async Task<List<Invoice>> ListInvoicesAsync(StripeInvoiceListOptions options)
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

    public Task<Invoice> CreateInvoicePreviewAsync(InvoiceCreatePreviewOptions options)
    {
        return _invoiceService.CreatePreviewAsync(options);
    }

    public async Task<List<Invoice>> SearchInvoiceAsync(InvoiceSearchOptions options)
    {
        return (await _invoiceService.SearchAsync(options)).Data;
    }

    public Task<Invoice> UpdateInvoiceAsync(string id, InvoiceUpdateOptions options)
    {
        return _invoiceService.UpdateAsync(id, options);
    }

    public Task<Invoice> FinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options)
    {
        return _invoiceService.FinalizeInvoiceAsync(id, options);
    }

    public Task<Invoice> SendInvoiceAsync(string id, InvoiceSendOptions options)
    {
        return _invoiceService.SendInvoiceAsync(id, options);
    }

    public Task<Invoice> PayInvoiceAsync(string id, InvoicePayOptions options = null)
    {
        return _invoiceService.PayAsync(id, options);
    }

    public Task<Invoice> DeleteInvoiceAsync(string id, InvoiceDeleteOptions options = null)
    {
        return _invoiceService.DeleteAsync(id, options);
    }

    public Task<Invoice> VoidInvoiceAsync(string id, InvoiceVoidOptions options = null)
    {
        return _invoiceService.VoidInvoiceAsync(id, options);
    }

    /********************
     ** PAYMENT METHOD **
     ********************/
    public IEnumerable<PaymentMethod> ListPaymentMethodsAutoPaging(PaymentMethodListOptions options)
    {
        return _paymentMethodService.ListAutoPaging(options);
    }

    public IAsyncEnumerable<PaymentMethod> ListPaymentMethodsAutoPagingAsync(PaymentMethodListOptions options)
        => _paymentMethodService.ListAutoPagingAsync(options);

    public Task<PaymentMethod> AttachPaymentMethodAsync(string id, PaymentMethodAttachOptions options = null)
    {
        return _paymentMethodService.AttachAsync(id, options);
    }

    public Task<PaymentMethod> DetachPaymentMethodAsync(string id, PaymentMethodDetachOptions options = null)
    {
        return _paymentMethodService.DetachAsync(id, options);
    }

    /************
     ** TAX ID **
     ************/
    public Task<TaxId> CreateTaxIdAsync(string id, TaxIdCreateOptions options)
    {
        return _taxIdService.CreateAsync(id, options);
    }

    public Task<TaxId> DeleteTaxIdAsync(string customerId, string taxIdId,
        TaxIdDeleteOptions options = null)
    {
        return _taxIdService.DeleteAsync(customerId, taxIdId);
    }

    /******************
     ** BANK ACCOUNT **
     ******************/
    public Task<BankAccount> CreateBankAccountAsync(string customerId, BankAccountCreateOptions options = null)
    {
        return _bankAccountService.CreateAsync(customerId, options);
    }

    public Task<BankAccount> DeleteBankAccountAsync(string customerId, string bankAccount, BankAccountDeleteOptions options = null)
    {
        return _bankAccountService.DeleteAsync(customerId, bankAccount, options);
    }

    /***********
     ** PRICE **
     ***********/
    public async Task<StripeList<Price>> ListPricesAsync(PriceListOptions options = null)
    {
        return await _priceService.ListAsync(options);
    }

    public Task<Price> GetPriceAsync(string id, PriceGetOptions options = null)
    {
        return _priceService.GetAsync(id, options);
    }

    /******************
     ** SETUP INTENT **
     ******************/
    public Task<SetupIntent> CreateSetupIntentAsync(SetupIntentCreateOptions options)
    {
        return _setupIntentService.CreateAsync(options);
    }

    public async Task<List<SetupIntent>> ListSetupIntentsAsync(SetupIntentListOptions options)
    {
        var setupIntents = await _setupIntentService.ListAsync(options);

        return setupIntents.Data;
    }

    public Task CancelSetupIntentAsync(string id, SetupIntentCancelOptions options = null)
    {
        return _setupIntentService.CancelAsync(id, options);
    }

    public Task<SetupIntent> GetSetupIntentAsync(string id, SetupIntentGetOptions options = null)
    {
        return _setupIntentService.GetAsync(id, options);
    }

    public Task VerifySetupIntentMicrodepositsAsync(string id, SetupIntentVerifyMicrodepositsOptions options)
    {
        return _setupIntentService.VerifyMicrodepositsAsync(id, options);
    }

    /*******************
     ** MISCELLANEOUS **
     *******************/
    public Task<StripeList<Charge>> ListChargesAsync(ChargeListOptions options)
    {
        return _chargeService.ListAsync(options);
    }

    public Task<Refund> CreateRefundAsync(RefundCreateOptions options)
    {
        return _refundService.CreateAsync(options);
    }

    public Task<Card> DeleteCardAsync(string customerId, string cardId, CardDeleteOptions options = null)
    {
        return _cardService.DeleteAsync(customerId, cardId, options);
    }

    public async Task<List<TestClock>> ListTestClocksAsync()
    {
        var items = new List<TestClock>();
        var options = new TestClockListOptions
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
