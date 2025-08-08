// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations;

public class StripeAdapter : IStripeAdapter
{
    private readonly Stripe.CustomerService _customerService;
    private readonly Stripe.SubscriptionService _subscriptionService;
    private readonly Stripe.InvoiceService _invoiceService;
    private readonly Stripe.PaymentMethodService _paymentMethodService;
    private readonly Stripe.TaxIdService _taxIdService;
    private readonly Stripe.ChargeService _chargeService;
    private readonly Stripe.RefundService _refundService;
    private readonly Stripe.CardService _cardService;
    private readonly Stripe.BankAccountService _bankAccountService;
    private readonly Stripe.PlanService _planService;
    private readonly Stripe.PriceService _priceService;
    private readonly Stripe.SetupIntentService _setupIntentService;
    private readonly Stripe.TestHelpers.TestClockService _testClockService;
    private readonly CustomerBalanceTransactionService _customerBalanceTransactionService;

    public StripeAdapter()
    {
        _customerService = new Stripe.CustomerService();
        _subscriptionService = new Stripe.SubscriptionService();
        _invoiceService = new Stripe.InvoiceService();
        _paymentMethodService = new Stripe.PaymentMethodService();
        _taxIdService = new Stripe.TaxIdService();
        _chargeService = new Stripe.ChargeService();
        _refundService = new Stripe.RefundService();
        _cardService = new Stripe.CardService();
        _bankAccountService = new Stripe.BankAccountService();
        _priceService = new Stripe.PriceService();
        _planService = new Stripe.PlanService();
        _setupIntentService = new SetupIntentService();
        _testClockService = new Stripe.TestHelpers.TestClockService();
        _customerBalanceTransactionService = new CustomerBalanceTransactionService();
    }

    /**************
     ** CUSTOMER **
     **************/
    public Task<Stripe.Customer> CreateCustomerAsync(Stripe.CustomerCreateOptions options)
    {
        return _customerService.CreateAsync(options);
    }

    public Task<Stripe.Customer> GetCustomerAsync(string id, Stripe.CustomerGetOptions options = null)
    {
        return _customerService.GetAsync(id, options);
    }

    public Task<Stripe.Customer> UpdateCustomerAsync(string id, Stripe.CustomerUpdateOptions options = null)
    {
        return _customerService.UpdateAsync(id, options);
    }

    public Task<Stripe.Customer> DeleteCustomerAsync(string id)
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
    public Task<Stripe.Subscription> CreateSubscriptionAsync(Stripe.SubscriptionCreateOptions options)
    {
        return _subscriptionService.CreateAsync(options);
    }

    public Task<Stripe.Subscription> GetSubscriptionAsync(string id, Stripe.SubscriptionGetOptions options = null)
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

    public Task<Stripe.Subscription> UpdateSubscriptionAsync(string id,
        Stripe.SubscriptionUpdateOptions options = null)
    {
        return _subscriptionService.UpdateAsync(id, options);
    }

    public Task<Stripe.Subscription> CancelSubscriptionAsync(string Id, Stripe.SubscriptionCancelOptions options = null)
    {
        return _subscriptionService.CancelAsync(Id, options);
    }

    public async Task<List<Stripe.Subscription>> ListSubscriptionsAsync(StripeSubscriptionListOptions options)
    {
        if (!options.SelectAll)
        {
            return (await _subscriptionService.ListAsync(options.ToStripeApiOptions())).Data;
        }

        options.Limit = 100;
        var items = new List<Stripe.Subscription>();
        await foreach (var i in _subscriptionService.ListAutoPagingAsync(options.ToStripeApiOptions()))
        {
            items.Add(i);
        }
        return items;
    }

    /*************
     ** INVOICE **
     *************/
    public Task<Stripe.Invoice> GetUpcomingInvoiceAsync(Stripe.UpcomingInvoiceOptions options)
    {
        return _invoiceService.UpcomingAsync(options);
    }

    public Task<Stripe.Invoice> GetInvoiceAsync(string id, Stripe.InvoiceGetOptions options)
    {
        return _invoiceService.GetAsync(id, options);
    }

    public async Task<List<Stripe.Invoice>> ListInvoicesAsync(StripeInvoiceListOptions options)
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

    public Task<Invoice> CreateInvoicePreviewAsync(InvoiceCreatePreviewOptions options)
    {
        return _invoiceService.CreatePreviewAsync(options);
    }

    public async Task<List<Stripe.Invoice>> SearchInvoiceAsync(InvoiceSearchOptions options)
    {
        return (await _invoiceService.SearchAsync(options)).Data;
    }

    public Task<Stripe.Invoice> UpdateInvoiceAsync(string id, Stripe.InvoiceUpdateOptions options)
    {
        return _invoiceService.UpdateAsync(id, options);
    }

    public Task<Stripe.Invoice> FinalizeInvoiceAsync(string id, Stripe.InvoiceFinalizeOptions options)
    {
        return _invoiceService.FinalizeInvoiceAsync(id, options);
    }

    public Task<Stripe.Invoice> SendInvoiceAsync(string id, Stripe.InvoiceSendOptions options)
    {
        return _invoiceService.SendInvoiceAsync(id, options);
    }

    public Task<Stripe.Invoice> PayInvoiceAsync(string id, Stripe.InvoicePayOptions options = null)
    {
        return _invoiceService.PayAsync(id, options);
    }

    public Task<Stripe.Invoice> DeleteInvoiceAsync(string id, Stripe.InvoiceDeleteOptions options = null)
    {
        return _invoiceService.DeleteAsync(id, options);
    }

    public Task<Stripe.Invoice> VoidInvoiceAsync(string id, Stripe.InvoiceVoidOptions options = null)
    {
        return _invoiceService.VoidInvoiceAsync(id, options);
    }

    /********************
     ** PAYMENT METHOD **
     ********************/
    public IEnumerable<Stripe.PaymentMethod> ListPaymentMethodsAutoPaging(Stripe.PaymentMethodListOptions options)
    {
        return _paymentMethodService.ListAutoPaging(options);
    }

    public IAsyncEnumerable<Stripe.PaymentMethod> ListPaymentMethodsAutoPagingAsync(Stripe.PaymentMethodListOptions options)
        => _paymentMethodService.ListAutoPagingAsync(options);

    public Task<Stripe.PaymentMethod> AttachPaymentMethodAsync(string id, Stripe.PaymentMethodAttachOptions options = null)
    {
        return _paymentMethodService.AttachAsync(id, options);
    }

    public Task<Stripe.PaymentMethod> DetachPaymentMethodAsync(string id, Stripe.PaymentMethodDetachOptions options = null)
    {
        return _paymentMethodService.DetachAsync(id, options);
    }

    /************
     ** TAX ID **
     ************/
    public Task<Stripe.TaxId> CreateTaxIdAsync(string id, Stripe.TaxIdCreateOptions options)
    {
        return _taxIdService.CreateAsync(id, options);
    }

    public Task<Stripe.TaxId> DeleteTaxIdAsync(string customerId, string taxIdId,
        Stripe.TaxIdDeleteOptions options = null)
    {
        return _taxIdService.DeleteAsync(customerId, taxIdId);
    }

    /******************
     ** BANK ACCOUNT **
     ******************/
    public Task<Stripe.BankAccount> CreateBankAccountAsync(string customerId, Stripe.BankAccountCreateOptions options = null)
    {
        return _bankAccountService.CreateAsync(customerId, options);
    }

    public Task<Stripe.BankAccount> DeleteBankAccountAsync(string customerId, string bankAccount, Stripe.BankAccountDeleteOptions options = null)
    {
        return _bankAccountService.DeleteAsync(customerId, bankAccount, options);
    }

    /***********
     ** PRICE **
     ***********/
    public async Task<Stripe.StripeList<Stripe.Price>> ListPricesAsync(Stripe.PriceListOptions options = null)
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
    public Task<Stripe.StripeList<Stripe.Charge>> ListChargesAsync(Stripe.ChargeListOptions options)
    {
        return _chargeService.ListAsync(options);
    }

    public Task<Stripe.Refund> CreateRefundAsync(Stripe.RefundCreateOptions options)
    {
        return _refundService.CreateAsync(options);
    }

    public Task<Stripe.Card> DeleteCardAsync(string customerId, string cardId, Stripe.CardDeleteOptions options = null)
    {
        return _cardService.DeleteAsync(customerId, cardId, options);
    }

    public async Task<List<Stripe.TestHelpers.TestClock>> ListTestClocksAsync()
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
