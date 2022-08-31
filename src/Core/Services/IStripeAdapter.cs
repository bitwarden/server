using Bit.Core.Models.BitStripe;

namespace Bit.Core.Services;

public interface IStripeAdapter
{
    Task<Stripe.Customer> CustomerCreateAsync(Stripe.CustomerCreateOptions customerCreateOptions);
    Task<Stripe.Customer> CustomerGetAsync(string id, Stripe.CustomerGetOptions options = null);
    Task<Stripe.Customer> CustomerUpdateAsync(string id, Stripe.CustomerUpdateOptions options = null);
    Task<Stripe.Customer> CustomerDeleteAsync(string id);
    Task<Stripe.Subscription> SubscriptionCreateAsync(Stripe.SubscriptionCreateOptions subscriptionCreateOptions);
    Task<Stripe.Subscription> SubscriptionGetAsync(string id, Stripe.SubscriptionGetOptions options = null);
    Task<List<Stripe.Subscription>> SubscriptionListAsync(StripeSubscriptionListOptions subscriptionSearchOptions);
    Task<Stripe.Subscription> SubscriptionUpdateAsync(string id, Stripe.SubscriptionUpdateOptions options = null);
    Task<Stripe.Subscription> SubscriptionCancelAsync(string Id, Stripe.SubscriptionCancelOptions options = null);
    Task<Stripe.Invoice> InvoiceUpcomingAsync(Stripe.UpcomingInvoiceOptions options);
    Task<Stripe.Invoice> InvoiceGetAsync(string id, Stripe.InvoiceGetOptions options);
    Task<Stripe.StripeList<Stripe.Invoice>> InvoiceListAsync(Stripe.InvoiceListOptions options);
    Task<Stripe.Invoice> InvoiceUpdateAsync(string id, Stripe.InvoiceUpdateOptions options);
    Task<Stripe.Invoice> InvoiceFinalizeInvoiceAsync(string id, Stripe.InvoiceFinalizeOptions options);
    Task<Stripe.Invoice> InvoiceSendInvoiceAsync(string id, Stripe.InvoiceSendOptions options);
    Task<Stripe.Invoice> InvoicePayAsync(string id, Stripe.InvoicePayOptions options = null);
    Task<Stripe.Invoice> InvoiceDeleteAsync(string id, Stripe.InvoiceDeleteOptions options = null);
    Task<Stripe.Invoice> InvoiceVoidInvoiceAsync(string id, Stripe.InvoiceVoidOptions options = null);
    IEnumerable<Stripe.PaymentMethod> PaymentMethodListAutoPaging(Stripe.PaymentMethodListOptions options);
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
    Task<List<Stripe.TestHelpers.TestClock>> TestClockListAsync();
}
