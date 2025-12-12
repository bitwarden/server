// FIXME: Update this file to be null safe and then delete the line below

#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;
using Stripe.Tax;
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
    private readonly RegistrationService _taxRegistrationService;

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
        _taxRegistrationService = new RegistrationService();
    }

    /**************
     ** CUSTOMER **
     **************/
    public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options) =>
        _customerService.CreateAsync(options);

    public Task DeleteCustomerDiscountAsync(string customerId, CustomerDeleteDiscountOptions options = null) =>
        _customerService.DeleteDiscountAsync(customerId, options);

    public Task<Customer> GetCustomerAsync(string id, CustomerGetOptions options = null) =>
        _customerService.GetAsync(id, options);

    public Task<Customer> UpdateCustomerAsync(string id, CustomerUpdateOptions options = null) =>
        _customerService.UpdateAsync(id, options);

    public Task<Customer> DeleteCustomerAsync(string id) =>
        _customerService.DeleteAsync(id);

    public async Task<List<PaymentMethod>> ListCustomerPaymentMethodsAsync(string id,
        CustomerPaymentMethodListOptions options = null)
    {
        var paymentMethods = await _customerService.ListPaymentMethodsAsync(id, options);
        return paymentMethods.Data;
    }

    public Task<CustomerBalanceTransaction> CreateCustomerBalanceTransactionAsync(string customerId,
        CustomerBalanceTransactionCreateOptions options) =>
        _customerBalanceTransactionService.CreateAsync(customerId, options);

    /******************
     ** SUBSCRIPTION **
     ******************/
    public Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions options) =>
        _subscriptionService.CreateAsync(options);

    public Task<Subscription> GetSubscriptionAsync(string id, SubscriptionGetOptions options = null) =>
        _subscriptionService.GetAsync(id, options);

    public Task<Subscription> UpdateSubscriptionAsync(string id,
        SubscriptionUpdateOptions options = null) =>
        _subscriptionService.UpdateAsync(id, options);

    public Task<Subscription> CancelSubscriptionAsync(string id, SubscriptionCancelOptions options = null) =>
        _subscriptionService.CancelAsync(id, options);

    /*************
     ** INVOICE **
     *************/
    public Task<Invoice> GetInvoiceAsync(string id, InvoiceGetOptions options) =>
        _invoiceService.GetAsync(id, options);

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

    public Task<Invoice> CreateInvoicePreviewAsync(InvoiceCreatePreviewOptions options) =>
        _invoiceService.CreatePreviewAsync(options);

    public async Task<List<Invoice>> SearchInvoiceAsync(InvoiceSearchOptions options) =>
        (await _invoiceService.SearchAsync(options)).Data;

    public Task<Invoice> UpdateInvoiceAsync(string id, InvoiceUpdateOptions options) =>
        _invoiceService.UpdateAsync(id, options);

    public Task<Invoice> FinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options) =>
        _invoiceService.FinalizeInvoiceAsync(id, options);

    public Task<Invoice> SendInvoiceAsync(string id, InvoiceSendOptions options) =>
        _invoiceService.SendInvoiceAsync(id, options);

    public Task<Invoice> PayInvoiceAsync(string id, InvoicePayOptions options = null) =>
        _invoiceService.PayAsync(id, options);

    public Task<Invoice> DeleteInvoiceAsync(string id, InvoiceDeleteOptions options = null) =>
        _invoiceService.DeleteAsync(id, options);

    public Task<Invoice> VoidInvoiceAsync(string id, InvoiceVoidOptions options = null) =>
        _invoiceService.VoidInvoiceAsync(id, options);

    /********************
     ** PAYMENT METHOD **
     ********************/
    public IEnumerable<PaymentMethod> ListPaymentMethodsAutoPaging(PaymentMethodListOptions options) =>
        _paymentMethodService.ListAutoPaging(options);

    public IAsyncEnumerable<PaymentMethod> ListPaymentMethodsAutoPagingAsync(PaymentMethodListOptions options)
        => _paymentMethodService.ListAutoPagingAsync(options);

    public Task<PaymentMethod> AttachPaymentMethodAsync(string id, PaymentMethodAttachOptions options = null) =>
        _paymentMethodService.AttachAsync(id, options);

    public Task<PaymentMethod> DetachPaymentMethodAsync(string id, PaymentMethodDetachOptions options = null) =>
        _paymentMethodService.DetachAsync(id, options);

    /************
     ** TAX ID **
     ************/
    public Task<TaxId> CreateTaxIdAsync(string id, TaxIdCreateOptions options) =>
        _taxIdService.CreateAsync(id, options);

    public Task<TaxId> DeleteTaxIdAsync(string customerId, string taxIdId,
        TaxIdDeleteOptions options = null) =>
        _taxIdService.DeleteAsync(customerId, taxIdId);

    /******************
     ** BANK ACCOUNT **
     ******************/
    public Task<BankAccount> CreateBankAccountAsync(string customerId, BankAccountCreateOptions options = null) =>
        _bankAccountService.CreateAsync(customerId, options);

    public Task<BankAccount> DeleteBankAccountAsync(string customerId, string bankAccount, BankAccountDeleteOptions options = null) =>
        _bankAccountService.DeleteAsync(customerId, bankAccount, options);

    /***********
     ** PRICE **
     ***********/
    public Task<StripeList<Price>> ListPricesAsync(PriceListOptions options = null) =>
        _priceService.ListAsync(options);

    public Task<Price> GetPriceAsync(string id, PriceGetOptions options = null) =>
        _priceService.GetAsync(id, options);

    /******************
     ** SETUP INTENT **
     ******************/
    public Task<SetupIntent> CreateSetupIntentAsync(SetupIntentCreateOptions options) =>
        _setupIntentService.CreateAsync(options);

    public async Task<List<SetupIntent>> ListSetupIntentsAsync(SetupIntentListOptions options) =>
        (await _setupIntentService.ListAsync(options)).Data;

    public Task CancelSetupIntentAsync(string id, SetupIntentCancelOptions options = null) =>
        _setupIntentService.CancelAsync(id, options);

    public Task<SetupIntent> GetSetupIntentAsync(string id, SetupIntentGetOptions options = null) =>
        _setupIntentService.GetAsync(id, options);

    public Task VerifySetupIntentMicrodepositsAsync(string id, SetupIntentVerifyMicrodepositsOptions options) =>
        _setupIntentService.VerifyMicrodepositsAsync(id, options);

    /*******************
     ** MISCELLANEOUS **
     *******************/
    public Task<StripeList<Charge>> ListChargesAsync(ChargeListOptions options) =>
        _chargeService.ListAsync(options);

    public Task<StripeList<Registration>> ListTaxRegistrationsAsync(RegistrationListOptions options = null) =>
        _taxRegistrationService.ListAsync(options);

    public Task<Refund> CreateRefundAsync(RefundCreateOptions options) =>
        _refundService.CreateAsync(options);

    public Task<Card> DeleteCardAsync(string customerId, string cardId, CardDeleteOptions options = null) =>
        _cardService.DeleteAsync(customerId, cardId, options);

    public async Task<List<TestClock>> ListTestClocksAsync()
    {
        var items = new List<TestClock>();
        var options = new TestClockListOptions { Limit = 100 };
        await foreach (var i in _testClockService.ListAutoPagingAsync(options))
        {
            items.Add(i);
        }

        return items;
    }
}
