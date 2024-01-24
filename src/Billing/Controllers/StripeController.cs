using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.Context;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Controllers;

[Route("stripe")]
public class StripeController : Controller
{
    private readonly BillingSettings _billingSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IOrganizationService _organizationService;
    private readonly IValidateSponsorshipCommand _validateSponsorshipCommand;
    private readonly IOrganizationSponsorshipRenewCommand _organizationSponsorshipRenewCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUserService _userService;
    private readonly IMailService _mailService;
    private readonly ILogger<StripeController> _logger;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IStripeEventService _stripeEventService;
    private readonly IStripeFacade _stripeFacade;
    private readonly IWebhookUtility _webhookUtility;

    public StripeController(
        IOptions<BillingSettings> billingSettings,
        IWebHostEnvironment hostingEnvironment,
        IOrganizationService organizationService,
        IValidateSponsorshipCommand validateSponsorshipCommand,
        IOrganizationSponsorshipRenewCommand organizationSponsorshipRenewCommand,
        IOrganizationRepository organizationRepository,
        ITransactionRepository transactionRepository,
        IUserService userService,
        IMailService mailService,
        IReferenceEventService referenceEventService,
        ILogger<StripeController> logger,
        ITaxRateRepository taxRateRepository,
        IUserRepository userRepository,
        ICurrentContext currentContext,
        IStripeEventService stripeEventService,
        IStripeFacade stripeFacade,
        IWebhookUtility webhookUtility)
    {
        _billingSettings = billingSettings?.Value;
        _hostingEnvironment = hostingEnvironment;
        _organizationService = organizationService;
        _validateSponsorshipCommand = validateSponsorshipCommand;
        _organizationSponsorshipRenewCommand = organizationSponsorshipRenewCommand;
        _organizationRepository = organizationRepository;
        _transactionRepository = transactionRepository;
        _userService = userService;
        _mailService = mailService;
        _referenceEventService = referenceEventService;
        _taxRateRepository = taxRateRepository;
        _userRepository = userRepository;
        _logger = logger;
        _currentContext = currentContext;
        _stripeEventService = stripeEventService;
        _stripeFacade = stripeFacade;
        _webhookUtility = webhookUtility;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> PostWebhook([FromQuery] string key)
    {
        if (!CoreHelpers.FixedTimeEquals(key, _billingSettings.StripeWebhookKey))
        {
            return new BadRequestResult();
        }

        Event parsedEvent;
        using (var sr = new StreamReader(HttpContext.Request.Body))
        {
            var json = await sr.ReadToEndAsync();
            parsedEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
                _billingSettings.StripeWebhookSecret,
                throwOnApiVersionMismatch: _billingSettings.StripeEventParseThrowMismatch);
        }

        if (string.IsNullOrWhiteSpace(parsedEvent?.Id))
        {
            _logger.LogWarning("No event id.");
            return new BadRequestResult();
        }

        if (_hostingEnvironment.IsProduction() && !parsedEvent.Livemode)
        {
            _logger.LogWarning("Getting test events in production.");
            return new BadRequestResult();
        }

        // If the customer and server cloud regions don't match, early return 200 to avoid unnecessary errors
        if (!await _stripeEventService.ValidateCloudRegion(parsedEvent))
        {
            return new OkResult();
        }

        var handlers = new List<IWebhookEventHandler>
        {
            new SubscriptionDeletedHandler(_organizationService, _userService,_stripeEventService,_webhookUtility),
            new SubscriptionUpdatedHandler(_organizationService, _userService,_stripeEventService,
                 _organizationSponsorshipRenewCommand,_webhookUtility),
            new UpcomingInvoiceHandler(_organizationRepository,_userService,_stripeEventService,_stripeFacade
             ,_logger,_taxRateRepository,_validateSponsorshipCommand,_mailService,_webhookUtility),
            new ChargeSucceededHandler(_transactionRepository,_logger,_stripeEventService,_webhookUtility),
            new ChargeRefundedHandler(_stripeEventService,_transactionRepository,_logger),
            new PaymentSucceededHandler(_stripeEventService,_organizationService,_organizationRepository,
                _referenceEventService,_currentContext,_userService,_userRepository,_webhookUtility),
            new PaymentFailedHandler(_stripeEventService,_webhookUtility),
            new InvoiceCreatedHandler(_stripeEventService,_webhookUtility),
            new PaymentMethodAttachedHandler(_stripeEventService,_webhookUtility,_logger),
            new CustomerUpdatedHandler(_stripeEventService,_webhookUtility,_organizationRepository,
                _referenceEventService, _currentContext)
        };

        var handler = handlers.FirstOrDefault(h => h.CanHandle(parsedEvent));
        if (handler != null)
        {
            await handler.HandleAsync(parsedEvent);
        }
        else
        {
            _logger.LogWarning("Unsupported event received: " + parsedEvent.Type);
        }

        // Return an OkResult to indicate successful processing
        return new OkResult();
    }
}
