// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;

namespace Bit.Core.Billing.Services;

public interface IStripeAdapter
{
    Task<Stripe.Customer> CreateCustomerAsync(Stripe.CustomerCreateOptions customerCreateOptions);
    Task<Stripe.Customer> GetCustomerAsync(string id, Stripe.CustomerGetOptions options = null);
    Task<Stripe.Customer> UpdateCustomerAsync(string id, Stripe.CustomerUpdateOptions options = null);
    Task<Stripe.Customer> DeleteCustomerAsync(string id);
    Task<List<PaymentMethod>> ListCustomerPaymentMethods(string id, CustomerListPaymentMethodsOptions options = null);
    Task<CustomerBalanceTransaction> CreateCustomerBalanceTransactionAsync(string customerId,
        CustomerBalanceTransactionCreateOptions options);
    Task<Stripe.Subscription> CreateSubscriptionAsync(Stripe.SubscriptionCreateOptions subscriptionCreateOptions);
    Task<Stripe.Subscription> GetSubscriptionAsync(string id, Stripe.SubscriptionGetOptions options = null);

    /// <summary>
    /// Retrieves a subscription object for a provider.
    /// </summary>
    /// <param name="id">The subscription ID.</param>
    /// <param name="providerId">The provider ID.</param>
    /// <param name="options">Additional options.</param>
    /// <returns>The subscription object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the subscription doesn't belong to the provider.</exception>
    Task<Stripe.Subscription> GetProviderSubscriptionAsync(string id, Guid providerId, Stripe.SubscriptionGetOptions options = null);

    Task<List<Stripe.Subscription>> ListSubscriptionsAsync(StripeSubscriptionListOptions subscriptionSearchOptions);
    Task<Stripe.Subscription> UpdateSubscriptionAsync(string id, Stripe.SubscriptionUpdateOptions options = null);
    Task<Stripe.Subscription> CancelSubscriptionAsync(string Id, Stripe.SubscriptionCancelOptions options = null);
    Task<Stripe.Invoice> GetUpcomingInvoiceAsync(Stripe.UpcomingInvoiceOptions options);
    Task<Stripe.Invoice> GetInvoiceAsync(string id, Stripe.InvoiceGetOptions options);
    Task<List<Stripe.Invoice>> ListInvoicesAsync(StripeInvoiceListOptions options);
    Task<Stripe.Invoice> CreateInvoicePreviewAsync(InvoiceCreatePreviewOptions options);
    Task<List<Stripe.Invoice>> SearchInvoiceAsync(InvoiceSearchOptions options);
    Task<Stripe.Invoice> UpdateInvoiceAsync(string id, Stripe.InvoiceUpdateOptions options);
    Task<Stripe.Invoice> FinalizeInvoiceAsync(string id, Stripe.InvoiceFinalizeOptions options);
    Task<Stripe.Invoice> SendInvoiceAsync(string id, Stripe.InvoiceSendOptions options);
    Task<Stripe.Invoice> PayInvoiceAsync(string id, Stripe.InvoicePayOptions options = null);
    Task<Stripe.Invoice> DeleteInvoiceAsync(string id, Stripe.InvoiceDeleteOptions options = null);
    Task<Stripe.Invoice> VoidInvoiceAsync(string id, Stripe.InvoiceVoidOptions options = null);
    IEnumerable<Stripe.PaymentMethod> ListPaymentMethodsAutoPaging(Stripe.PaymentMethodListOptions options);
    IAsyncEnumerable<Stripe.PaymentMethod> ListPaymentMethodsAutoPagingAsync(Stripe.PaymentMethodListOptions options);
    Task<Stripe.PaymentMethod> AttachPaymentMethodAsync(string id, Stripe.PaymentMethodAttachOptions options = null);
    Task<Stripe.PaymentMethod> DetachPaymentMethodAsync(string id, Stripe.PaymentMethodDetachOptions options = null);
    Task<Stripe.TaxId> CreateTaxIdAsync(string id, Stripe.TaxIdCreateOptions options);
    Task<Stripe.TaxId> DeleteTaxIdAsync(string customerId, string taxIdId, Stripe.TaxIdDeleteOptions options = null);
    Task<Stripe.StripeList<Stripe.Charge>> ListChargesAsync(Stripe.ChargeListOptions options);
    Task<Stripe.Refund> CreateRefundAsync(Stripe.RefundCreateOptions options);
    Task<Stripe.Card> DeleteCardAsync(string customerId, string cardId, Stripe.CardDeleteOptions options = null);
    Task<Stripe.BankAccount> CreateBankAccountAsync(string customerId, Stripe.BankAccountCreateOptions options = null);
    Task<Stripe.BankAccount> DeleteBankAccountAsync(string customerId, string bankAccount, Stripe.BankAccountDeleteOptions options = null);
    Task<Stripe.StripeList<Stripe.Price>> ListPricesAsync(Stripe.PriceListOptions options = null);
    Task<SetupIntent> CreateSetupIntentAsync(SetupIntentCreateOptions options);
    Task<List<SetupIntent>> ListSetupIntentsAsync(SetupIntentListOptions options);
    Task CancelSetupIntentAsync(string id, SetupIntentCancelOptions options = null);
    Task<SetupIntent> GetSetupIntentAsync(string id, SetupIntentGetOptions options = null);
    Task VerifySetupIntentMicrodepositsAsync(string id, SetupIntentVerifyMicrodepositsOptions options);
    Task<List<Stripe.TestHelpers.TestClock>> ListTestClocksAsync();
    Task<Price> GetPriceAsync(string id, PriceGetOptions options = null);
}
