using Bit.Core.Models.BitStripe;
using Stripe;

namespace Bit.Core.Services;

// ReSharper disable InconsistentNaming
public interface IStripeAdapter
{
    #nullable enable

    #region Customers

    Task<Customer> CustomerCreateAsync(CustomerCreateOptions options);
    Task<Customer> CustomerDeleteAsync(string id);
    Task<Customer> CustomerGetAsync(string id, CustomerGetOptions? options = null);
    Task<List<PaymentMethod>> CustomerListPaymentMethodsAsync(string id, CustomerListPaymentMethodsOptions? options = null);
    Task<Customer?> CustomerTryGetAsync(string id, CustomerGetOptions? options = null);
    Task<Customer> CustomerUpdateAsync(string id, CustomerUpdateOptions options);

    #endregion

    #region Subscriptions

    Task<Subscription> SubscriptionCancelAsync(string id, SubscriptionCancelOptions? options = null);
    Task<Subscription> SubscriptionCreateAsync(SubscriptionCreateOptions options);
    Task<Subscription> SubscriptionGetAsync(string id, SubscriptionGetOptions? options = null);
    Task<List<Subscription>> SubscriptionListAsync(StripeSubscriptionListOptions options);
    Task<Subscription?> SubscriptionTryGetAsync(string id, SubscriptionGetOptions? options = null);
    Task<Subscription> SubscriptionUpdateAsync(string id, SubscriptionUpdateOptions options);

    #endregion

    #nullable disable

    Task<Stripe.Invoice> InvoiceUpcomingAsync(Stripe.UpcomingInvoiceOptions options);
    Task<Stripe.Invoice> InvoiceGetAsync(string id, Stripe.InvoiceGetOptions options);
    Task<List<Stripe.Invoice>> InvoiceListAsync(StripeInvoiceListOptions options);
    Task<List<Stripe.Invoice>> InvoiceSearchAsync(InvoiceSearchOptions options);
    Task<Stripe.Invoice> InvoiceUpdateAsync(string id, Stripe.InvoiceUpdateOptions options);
    Task<Stripe.Invoice> InvoiceFinalizeInvoiceAsync(string id, Stripe.InvoiceFinalizeOptions options);
    Task<Stripe.Invoice> InvoiceSendInvoiceAsync(string id, Stripe.InvoiceSendOptions options);
    Task<Stripe.Invoice> InvoicePayAsync(string id, Stripe.InvoicePayOptions options = null);
    Task<Stripe.Invoice> InvoiceDeleteAsync(string id, Stripe.InvoiceDeleteOptions options = null);
    Task<Stripe.Invoice> InvoiceVoidInvoiceAsync(string id, Stripe.InvoiceVoidOptions options = null);
    IEnumerable<Stripe.PaymentMethod> PaymentMethodListAutoPaging(Stripe.PaymentMethodListOptions options);
    IAsyncEnumerable<Stripe.PaymentMethod> PaymentMethodListAutoPagingAsync(Stripe.PaymentMethodListOptions options);
    Task<Stripe.PaymentMethod> PaymentMethodAttachAsync(string id, Stripe.PaymentMethodAttachOptions options = null);
    Task<Stripe.PaymentMethod> PaymentMethodDetachAsync(string id, Stripe.PaymentMethodDetachOptions options = null);
    Task<Stripe.TaxRate> TaxRateCreateAsync(Stripe.TaxRateCreateOptions options);
    Task<Stripe.TaxRate> TaxRateUpdateAsync(string id, Stripe.TaxRateUpdateOptions options);
    Task<Stripe.TaxId> TaxIdCreateAsync(string id, Stripe.TaxIdCreateOptions options);
    Task<Stripe.TaxId> TaxIdDeleteAsync(string customerId, string taxIdId, Stripe.TaxIdDeleteOptions options = null);
    Task<Stripe.StripeList<Stripe.Charge>> ChargeListAsync(Stripe.ChargeListOptions options);
    Task<Stripe.Refund> RefundCreateAsync(Stripe.RefundCreateOptions options);
    Task<Stripe.Card> CardDeleteAsync(string customerId, string cardId, Stripe.CardDeleteOptions options = null);
    Task<Stripe.BankAccount> BankAccountCreateAsync(string customerId, Stripe.BankAccountCreateOptions options = null);
    Task<Stripe.BankAccount> BankAccountDeleteAsync(string customerId, string bankAccount, Stripe.BankAccountDeleteOptions options = null);
    Task<Stripe.StripeList<Stripe.Price>> PriceListAsync(Stripe.PriceListOptions options = null);
    Task<SetupIntent> SetupIntentCreate(SetupIntentCreateOptions options);
    Task<List<SetupIntent>> SetupIntentList(SetupIntentListOptions options);
    Task SetupIntentCancel(string id, SetupIntentCancelOptions options = null);
    Task<SetupIntent> SetupIntentGet(string id, SetupIntentGetOptions options = null);
    Task SetupIntentVerifyMicroDeposit(string id, SetupIntentVerifyMicrodepositsOptions options);
    Task<List<Stripe.TestHelpers.TestClock>> TestClockListAsync();
}
