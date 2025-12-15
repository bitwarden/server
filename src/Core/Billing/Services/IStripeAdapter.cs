// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.BitStripe;
using Stripe;
using Stripe.Tax;

namespace Bit.Core.Billing.Services;

public interface IStripeAdapter
{
    Task<Customer> CreateCustomerAsync(CustomerCreateOptions customerCreateOptions);
    Task<Customer> GetCustomerAsync(string id, CustomerGetOptions options = null);
    Task<Customer> UpdateCustomerAsync(string id, CustomerUpdateOptions options = null);
    Task<Customer> DeleteCustomerAsync(string id);
    Task<List<PaymentMethod>> ListCustomerPaymentMethodsAsync(string id, CustomerPaymentMethodListOptions options = null);
    Task<CustomerBalanceTransaction> CreateCustomerBalanceTransactionAsync(string customerId,
        CustomerBalanceTransactionCreateOptions options);
    Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions subscriptionCreateOptions);
    Task<Subscription> GetSubscriptionAsync(string id, SubscriptionGetOptions options = null);
    Task<StripeList<Registration>> ListTaxRegistrationsAsync(RegistrationListOptions options = null);
    Task DeleteCustomerDiscountAsync(string customerId, CustomerDeleteDiscountOptions options = null);
    Task<Subscription> UpdateSubscriptionAsync(string id, SubscriptionUpdateOptions options = null);
    Task<Subscription> CancelSubscriptionAsync(string id, SubscriptionCancelOptions options = null);
    Task<Invoice> GetInvoiceAsync(string id, InvoiceGetOptions options);
    Task<List<Invoice>> ListInvoicesAsync(StripeInvoiceListOptions options);
    Task<Invoice> CreateInvoicePreviewAsync(InvoiceCreatePreviewOptions options);
    Task<List<Invoice>> SearchInvoiceAsync(InvoiceSearchOptions options);
    Task<Invoice> UpdateInvoiceAsync(string id, InvoiceUpdateOptions options);
    Task<Invoice> FinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options);
    Task<Invoice> SendInvoiceAsync(string id, InvoiceSendOptions options);
    Task<Invoice> PayInvoiceAsync(string id, InvoicePayOptions options = null);
    Task<Invoice> DeleteInvoiceAsync(string id, InvoiceDeleteOptions options = null);
    Task<Invoice> VoidInvoiceAsync(string id, InvoiceVoidOptions options = null);
    IEnumerable<PaymentMethod> ListPaymentMethodsAutoPaging(PaymentMethodListOptions options);
    IAsyncEnumerable<PaymentMethod> ListPaymentMethodsAutoPagingAsync(PaymentMethodListOptions options);
    Task<PaymentMethod> AttachPaymentMethodAsync(string id, PaymentMethodAttachOptions options = null);
    Task<PaymentMethod> DetachPaymentMethodAsync(string id, PaymentMethodDetachOptions options = null);
    Task<TaxId> CreateTaxIdAsync(string id, TaxIdCreateOptions options);
    Task<TaxId> DeleteTaxIdAsync(string customerId, string taxIdId, TaxIdDeleteOptions options = null);
    Task<StripeList<Charge>> ListChargesAsync(ChargeListOptions options);
    Task<Refund> CreateRefundAsync(RefundCreateOptions options);
    Task<Card> DeleteCardAsync(string customerId, string cardId, CardDeleteOptions options = null);
    Task<BankAccount> DeleteBankAccountAsync(string customerId, string bankAccount, BankAccountDeleteOptions options = null);
    Task<SetupIntent> CreateSetupIntentAsync(SetupIntentCreateOptions options);
    Task<List<SetupIntent>> ListSetupIntentsAsync(SetupIntentListOptions options);
    Task CancelSetupIntentAsync(string id, SetupIntentCancelOptions options = null);
    Task<SetupIntent> GetSetupIntentAsync(string id, SetupIntentGetOptions options = null);
    Task<Price> GetPriceAsync(string id, PriceGetOptions options = null);
}
