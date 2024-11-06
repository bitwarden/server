using Bit.Core.Models.BitStripe;
using Stripe;

namespace Bit.Core.Services;

public interface IStripeAdapter
{
    Task<Stripe.Customer> CustomerCreateAsync(Stripe.CustomerCreateOptions customerCreateOptions);
    Task<Stripe.Customer> CustomerGetAsync(string id, Stripe.CustomerGetOptions options = null);
    Task<Stripe.Customer> CustomerUpdateAsync(string id, Stripe.CustomerUpdateOptions options = null);
    Task<Stripe.Customer> CustomerDeleteAsync(string id);
    Task<List<PaymentMethod>> CustomerListPaymentMethods(string id, CustomerListPaymentMethodsOptions options = null);
    Task<CustomerBalanceTransaction> CustomerBalanceTransactionCreate(string customerId,
        CustomerBalanceTransactionCreateOptions options);
    Task<Stripe.Subscription> SubscriptionCreateAsync(Stripe.SubscriptionCreateOptions subscriptionCreateOptions);
    Task<Stripe.Subscription> SubscriptionGetAsync(string id, Stripe.SubscriptionGetOptions options = null);

    /// <summary>
    /// Retrieves a subscription object for a provider.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="providerId">The provider ID.</param>
    /// <param name="options">Additional options.</param>
    /// <returns>The subscription object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the subscription doesn't belong to the provider.</exception>
    Task<Stripe.Subscription> ProviderSubscriptionGetAsync(string id, Guid providerId, Stripe.SubscriptionGetOptions options = null);

    Task<List<Stripe.Subscription>> SubscriptionListAsync(StripeSubscriptionListOptions subscriptionSearchOptions);
    Task<Stripe.Subscription> SubscriptionUpdateAsync(string id, Stripe.SubscriptionUpdateOptions options = null);
    Task<Stripe.Subscription> SubscriptionCancelAsync(string Id, Stripe.SubscriptionCancelOptions options = null);
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
