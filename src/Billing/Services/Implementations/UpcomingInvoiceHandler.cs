using Bit.Billing.Constants;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Stripe;
using Event = Stripe.Event;
using TaxRate = Bit.Core.Entities.TaxRate;

namespace Bit.Billing.Services.Implementations;

public class UpcomingInvoiceHandler : IUpcomingInvoiceHandler
{
    private readonly ILogger<StripeEventProcessor> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IFeatureService _featureService;
    private readonly IMailService _mailService;
    private readonly IProviderRepository _providerRepository;
    private readonly IValidateSponsorshipCommand _validateSponsorshipCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly ITaxRateRepository _taxRateRepository;

    public UpcomingInvoiceHandler(
        ILogger<StripeEventProcessor> logger,
        IStripeEventService stripeEventService,
        IUserService userService,
        IStripeFacade stripeFacade,
        IFeatureService featureService,
        IMailService mailService,
        IProviderRepository providerRepository,
        IValidateSponsorshipCommand validateSponsorshipCommand,
        IOrganizationRepository organizationRepository,
        IStripeEventUtilityService stripeEventUtilityService,
        ITaxRateRepository taxRateRepository)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _userService = userService;
        _stripeFacade = stripeFacade;
        _featureService = featureService;
        _mailService = mailService;
        _providerRepository = providerRepository;
        _validateSponsorshipCommand = validateSponsorshipCommand;
        _organizationRepository = organizationRepository;
        _stripeEventUtilityService = stripeEventUtilityService;
        _taxRateRepository = taxRateRepository;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.UpcomingInvoice"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    /// <exception cref="Exception"></exception>
    public async Task HandleAsync(Event parsedEvent)
    {
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

        var pm5766AutomaticTaxIsEnabled = _featureService.IsEnabled(FeatureFlagKeys.PM5766AutomaticTax);
        if (pm5766AutomaticTaxIsEnabled)
        {
            var customerGetOptions = new CustomerGetOptions();
            customerGetOptions.AddExpand("tax");
            var customer = await _stripeFacade.GetCustomer(subscription.CustomerId, customerGetOptions);
            if (!subscription.AutomaticTax.Enabled &&
                customer.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported)
            {
                subscription = await _stripeFacade.UpdateSubscription(subscription.Id,
                    new SubscriptionUpdateOptions
                    {
                        DefaultTaxRates = [],
                        AutomaticTax = new SubscriptionAutomaticTaxOptions { Enabled = true }
                    });
            }
        }

        var updatedSubscription = pm5766AutomaticTaxIsEnabled
            ? subscription
            : await VerifyCorrectTaxRateForChargeAsync(invoice, subscription);

        var (organizationId, userId, providerId) = _stripeEventUtilityService.GetIdsFromMetadata(updatedSubscription.Metadata);

        var invoiceLineItemDescriptions = invoice.Lines.Select(i => i.Description).ToList();

        if (organizationId.HasValue)
        {
            if (_stripeEventUtilityService.IsSponsoredSubscription(updatedSubscription))
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

            if (user?.Premium == true)
            {
                await SendEmails(new List<string> { user.Email });
            }
        }
        else if (providerId.HasValue)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId.Value);

            if (provider == null)
            {
                _logger.LogError(
                    "Received invoice.Upcoming webhook ({EventID}) for Provider ({ProviderID}) that does not exist",
                    parsedEvent.Id,
                    providerId.Value);

                return;
            }

            await SendEmails(new List<string> { provider.BillingEmail });

        }

        return;

        /*
         * Sends emails to the given email addresses.
         */
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
    }

    private async Task<Stripe.Subscription> VerifyCorrectTaxRateForChargeAsync(Invoice invoice, Stripe.Subscription subscription)
    {
        if (string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.Country) ||
            string.IsNullOrWhiteSpace(invoice?.CustomerAddress?.PostalCode))
        {
            return subscription;
        }

        var localBitwardenTaxRates = await _taxRateRepository.GetByLocationAsync(
            new TaxRate()
            {
                Country = invoice.CustomerAddress.Country,
                PostalCode = invoice.CustomerAddress.PostalCode
            }
        );

        if (!localBitwardenTaxRates.Any())
        {
            return subscription;
        }

        var stripeTaxRate = await _stripeFacade.GetTaxRate(localBitwardenTaxRates.First().Id);
        if (stripeTaxRate == null || subscription.DefaultTaxRates.Any(x => x == stripeTaxRate))
        {
            return subscription;
        }

        subscription.DefaultTaxRates = [stripeTaxRate];

        var subscriptionOptions = new SubscriptionUpdateOptions { DefaultTaxRates = [stripeTaxRate.Id] };
        subscription = await _stripeFacade.UpdateSubscription(subscription.Id, subscriptionOptions);

        return subscription;
    }

    private static bool OrgPlanForInvoiceNotifications(Organization org) => StaticStore.GetPlan(org.PlanType).IsAnnual;
}
