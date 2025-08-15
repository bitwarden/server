// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;

namespace Bit.Core.Services;

public interface IStripeAdapter
{
    Task<Customer> CustomerCreateAsync(CustomerCreateOptions customerCreateOptions);
    Task CustomerDeleteDiscountAsync(string customerId, CustomerDeleteDiscountOptions options = null);
    Task<Customer> CustomerGetAsync(string id, CustomerGetOptions options = null);
    Task<Customer> CustomerUpdateAsync(string id, CustomerUpdateOptions options = null);
    Task<Customer> CustomerDeleteAsync(string id);
    Task<List<PaymentMethod>> CustomerListPaymentMethods(string id, CustomerPaymentMethodListOptions options = null);
    Task<CustomerBalanceTransaction> CustomerBalanceTransactionCreate(string customerId,
        CustomerBalanceTransactionCreateOptions options);
    Task<Subscription> SubscriptionCreateAsync(SubscriptionCreateOptions subscriptionCreateOptions);
    Task<Subscription> SubscriptionGetAsync(string id, SubscriptionGetOptions options = null);

    /// <summary>
    /// Retrieves a subscription object for a provider.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="providerId">The provider ID.</param>
    /// <param name="options">Additional options.</param>
    /// <returns>The subscription object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the subscription doesn't belong to the provider.</exception>
    Task<Subscription> ProviderSubscriptionGetAsync(string id, Guid providerId, SubscriptionGetOptions options = null);

    Task<Subscription> SubscriptionUpdateAsync(string id, SubscriptionUpdateOptions options = null);
    Task<Subscription> SubscriptionCancelAsync(string Id, SubscriptionCancelOptions options = null);
    Task<Invoice> InvoiceGetAsync(string id, InvoiceGetOptions options);
    Task<List<Invoice>> InvoiceListAsync(StripeInvoiceListOptions options);
    Task<Invoice> InvoiceCreatePreviewAsync(InvoiceCreatePreviewOptions options);
    Task<List<Invoice>> InvoiceSearchAsync(InvoiceSearchOptions options);
    Task<Invoice> InvoiceUpdateAsync(string id, InvoiceUpdateOptions options);
    Task<Invoice> InvoiceFinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options);
    Task<Invoice> InvoiceSendInvoiceAsync(string id, InvoiceSendOptions options);
    Task<Invoice> InvoicePayAsync(string id, InvoicePayOptions options = null);
    Task<Invoice> InvoiceDeleteAsync(string id, InvoiceDeleteOptions options = null);
    Task<Invoice> InvoiceVoidInvoiceAsync(string id, InvoiceVoidOptions options = null);
    IEnumerable<PaymentMethod> PaymentMethodListAutoPaging(PaymentMethodListOptions options);
    IAsyncEnumerable<PaymentMethod> PaymentMethodListAutoPagingAsync(PaymentMethodListOptions options);
    Task<PaymentMethod> PaymentMethodAttachAsync(string id, PaymentMethodAttachOptions options = null);
    Task<PaymentMethod> PaymentMethodDetachAsync(string id, PaymentMethodDetachOptions options = null);
    Task<TaxId> TaxIdCreateAsync(string id, TaxIdCreateOptions options);
    Task<TaxId> TaxIdDeleteAsync(string customerId, string taxIdId, TaxIdDeleteOptions options = null);
    Task<StripeList<Charge>> ChargeListAsync(ChargeListOptions options);
    Task<Refund> RefundCreateAsync(RefundCreateOptions options);
    Task<Card> CardDeleteAsync(string customerId, string cardId, CardDeleteOptions options = null);
    Task<BankAccount> BankAccountCreateAsync(string customerId, BankAccountCreateOptions options = null);
    Task<BankAccount> BankAccountDeleteAsync(string customerId, string bankAccount, BankAccountDeleteOptions options = null);
    Task<StripeList<Price>> PriceListAsync(PriceListOptions options = null);
    Task<SetupIntent> SetupIntentCreate(SetupIntentCreateOptions options);
    Task<List<SetupIntent>> SetupIntentList(SetupIntentListOptions options);
    Task SetupIntentCancel(string id, SetupIntentCancelOptions options = null);
    Task<SetupIntent> SetupIntentGet(string id, SetupIntentGetOptions options = null);
    Task SetupIntentVerifyMicroDeposit(string id, SetupIntentVerifyMicrodepositsOptions options);
    Task<List<Stripe.TestHelpers.TestClock>> TestClockListAsync();
    Task<Price> PriceGetAsync(string id, PriceGetOptions options = null);
}
