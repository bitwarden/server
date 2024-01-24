using Bit.Billing.Constants;
using Bit.Billing.Controllers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Stripe;
using Event = Stripe.Event;
using Subscription = Stripe.Subscription;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Billing.Services.Implementations;


public class UpcomingInvoiceHandler : IWebhookEventHandler
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly ILogger<StripeController> _logger;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IValidateSponsorshipCommand _validateSponsorshipCommand;
    private readonly IMailService _mailService;
    private readonly IWebhookUtility _webhookUtility;


    public UpcomingInvoiceHandler(IOrganizationRepository organizationRepository,
        IUserService userService,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        ILogger<StripeController> logger,
        ITaxRateRepository taxRateRepository,
        IValidateSponsorshipCommand validateSponsorshipCommand,
        IMailService mailService,
        IWebhookUtility webhookUtility)
    {
        _organizationRepository = organizationRepository;
        _userService = userService;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _logger = logger;
        _taxRateRepository = taxRateRepository;
        _validateSponsorshipCommand = validateSponsorshipCommand;
        _mailService = mailService;
        _webhookUtility = webhookUtility;
    }

    public bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.UpcomingInvoice);
    }

    public async Task HandleAsync(Event parsedEvent)
    {
        // Handle UpcomingInvoice event
        var invoice = await _stripeEventService.GetInvoice(parsedEvent);

        if (string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            _logger.LogWarning("Received 'invoice.upcoming' Event with ID '{eventId}' that did not include a Subscription ID", parsedEvent.Id);
            return;
        }

        var subscription = await _stripeFacade.GetSubscription(invoice.SubscriptionId);

        if (subscription == null)
        {
            throw new Exception(
                $"Received null Subscription from Stripe for ID '{invoice.SubscriptionId}' while processing Event with ID '{parsedEvent.Id}'");
        }

        var updatedSubscription = await VerifyCorrectTaxRateForCharge(invoice, subscription);

        var (organizationId, userId) = _webhookUtility.GetIdsFromMetaData(updatedSubscription.Metadata);

        var invoiceLineItemDescriptions = invoice.Lines.Select(i => i.Description).ToList();

        async Task SendEmails(IEnumerable<string> emails)
        {
            var validEmails = emails.Where(e => !string.IsNullOrEmpty(e));

            if (invoice.NextPaymentAttempt.HasValue)
            {
                await _mailService.SendInvoiceUpcoming(
                    validEmails,
                    invoice.AmountDue / 100M,
                    invoice.NextPaymentAttempt.Value,
                    invoiceLineItemDescriptions,
                    true);
            }
        }

        if (organizationId.HasValue)
        {
            if (_webhookUtility.IsSponsoredSubscription(updatedSubscription))
            {
                await _validateSponsorshipCommand.ValidateSponsorshipAsync(organizationId.Value);
            }

            var organization = await _organizationRepository.GetByIdAsync(organizationId.Value);

            if (organization == null || !OrgPlanForInvoiceNotifications(organization))
            {
                return;
            }

            await SendEmails(new List<string> { organization.BillingEmail });

            /*
             * TODO: https://bitwarden.atlassian.net/browse/PM-4862
             * Disabling this as part of a hot fix. It needs to check whether the organization
             * belongs to a Reseller provider and only send an email to the organization owners if it does.
             * It also requires a new email template as the current one contains too much billing information.
             */

            // var ownerEmails = await _organizationRepository.GetOwnerEmailAddressesById(organization.Id);

            // await SendEmails(ownerEmails);
        }
        else if (userId.HasValue)
        {
            var user = await _userService.GetUserByIdAsync(userId.Value);

            if (user.Premium)
            {
                await SendEmails(new List<string> { user.Email });
            }
        }
    }

    private async Task<Subscription> VerifyCorrectTaxRateForCharge(Invoice invoice, Subscription subscription)
    {
        if (!string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.Country) && !string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.PostalCode))
        {
            var localBitwardenTaxRates = await _taxRateRepository.GetByLocationAsync(
                new TaxRate()
                {
                    Country = invoice.CustomerAddress.Country,
                    PostalCode = invoice.CustomerAddress.PostalCode
                }
            );

            if (localBitwardenTaxRates.Any())
            {
                var stripeTaxRate = await new TaxRateService().GetAsync(localBitwardenTaxRates.First().Id);
                if (stripeTaxRate != null && !subscription.DefaultTaxRates.Any(x => x == stripeTaxRate))
                {
                    subscription.DefaultTaxRates = new List<Stripe.TaxRate> { stripeTaxRate };
                    var subscriptionOptions = new SubscriptionUpdateOptions() { DefaultTaxRates = new List<string>() { stripeTaxRate.Id } };
                    subscription = await new SubscriptionService().UpdateAsync(subscription.Id, subscriptionOptions);
                }
            }
        }
        return subscription;
    }

    private static bool OrgPlanForInvoiceNotifications(Organization org) => StaticStore.GetPlan(org.PlanType).IsAnnual;
}
