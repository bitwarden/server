using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class StripeAdapter : IStripeAdapter
    {
        private readonly Stripe.CustomerService _customerService;
        private readonly Stripe.SubscriptionService _subscriptionService;
        private readonly Stripe.InvoiceService _invoiceService;
        private readonly Stripe.PaymentMethodService _paymentMethodService;
        private readonly Stripe.TaxRateService _taxRateService;
        private readonly Stripe.TaxIdService _taxIdService;
        private readonly Stripe.ChargeService _chargeService;
        private readonly Stripe.RefundService _refundService;
        private readonly Stripe.CardService _cardService;
        private readonly Stripe.BankAccountService _bankAccountService;

        public StripeAdapter()
        {
            _customerService = new Stripe.CustomerService();
            _subscriptionService = new Stripe.SubscriptionService();
            _invoiceService = new Stripe.InvoiceService();
            _paymentMethodService = new Stripe.PaymentMethodService();
            _taxRateService = new Stripe.TaxRateService();
            _taxIdService = new Stripe.TaxIdService();
            _chargeService = new Stripe.ChargeService();
            _refundService = new Stripe.RefundService();
            _cardService = new Stripe.CardService();
            _bankAccountService = new Stripe.BankAccountService();
        }

        public Task<Stripe.Customer> CustomerCreateAsync(Stripe.CustomerCreateOptions options)
        {
            return _customerService.CreateAsync(options);
        }

        public Task<Stripe.Customer> CustomerGetAsync(string id, Stripe.CustomerGetOptions options = null)
        {
            return _customerService.GetAsync(id, options);
        }

        public Task<Stripe.Customer> CustomerUpdateAsync(string id, Stripe.CustomerUpdateOptions options = null)
        {
            return _customerService.UpdateAsync(id, options);
        }

        public Task<Stripe.Customer> CustomerDeleteAsync(string id)
        {
            return _customerService.DeleteAsync(id);
        }

        public Task<Stripe.Subscription> SubscriptionCreateAsync(Stripe.SubscriptionCreateOptions options)
        {
            return _subscriptionService.CreateAsync(options);
        }

        public Task<Stripe.Subscription> SubscriptionGetAsync(string id, Stripe.SubscriptionGetOptions options = null)
        {
            return _subscriptionService.GetAsync(id, options);
        }

        public Task<Stripe.Subscription> SubscriptionUpdateAsync(string id,
            Stripe.SubscriptionUpdateOptions options = null)
        {
            return _subscriptionService.UpdateAsync(id, options);
        }

        public Task<Stripe.Subscription> SubscriptionCancelAsync(string Id, Stripe.SubscriptionCancelOptions options)
        {
            return _subscriptionService.CancelAsync(Id, options);
        }

        public Task<Stripe.Invoice> InvoiceUpcomingAsync(Stripe.UpcomingInvoiceOptions options)
        {
            return _invoiceService.UpcomingAsync(options);
        }

        public Task<Stripe.Invoice> InvoiceGetAsync(string id, Stripe.InvoiceGetOptions options)
        {
            return _invoiceService.GetAsync(id, options);
        }

        public Task<Stripe.StripeList<Stripe.Invoice>> InvoiceListAsync(Stripe.InvoiceListOptions options)
        {
            return _invoiceService.ListAsync(options);
        }

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
    }
}
