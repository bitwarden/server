using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models.Api.Response.Premium;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Core.Billing.Premium.Commands;

/// <summary>
/// Creates a Stripe Checkout Session for a user to purchase a premium subscription.
/// </summary>
public interface ICreatePremiumCheckoutSessionCommand
{
    /// <summary>
    /// Creates a Stripe Checkout Session for a user to purchase a premium subscription.
    /// </summary>
    /// <param name="user"> The user for whom the Checkout Session is being created. </param>
    /// <param name="originatingAppVersion"> The version of the application initiating the Checkout Session. </param>
    /// <param name="originatingPlatform"> The platform (e.g., ios, android) from which the Checkout Session is initiated. </param>
    /// <returns> The url of the created Checkout Session. </returns>
    Task<BillingCommandResult<PremiumCheckoutSessionResponseModel>> Run(User user, string originatingAppVersion, string originatingPlatform);
}

public class CreatePremiumCheckoutSessionCommand(
    IStripeAdapter stripeAdapter,
    IPricingClient pricingClient,
    ISubscriberService subscriberService,
    IGlobalSettings globalSettings,
    ILogger<CreatePremiumCheckoutSessionCommand> logger
) : BaseBillingCommand<CreatePremiumCheckoutSessionCommand>(logger), ICreatePremiumCheckoutSessionCommand
{
    private readonly IStripeAdapter stripeAdapter = stripeAdapter;
    private readonly IPricingClient pricingClient = pricingClient;
    private readonly ISubscriberService subscriberService = subscriberService;
    private readonly IGlobalSettings globalSettings = globalSettings;

    public Task<BillingCommandResult<PremiumCheckoutSessionResponseModel>>
        Run(User user, string originatingAppVersion, string originatingPlatform) => HandleAsync<PremiumCheckoutSessionResponseModel>(async () =>
    {
        if (user.Premium)
        {
            return new BadRequest("User is already a premium user.");
        }

        // If the user doesn't have a Stripe customer ID, create one.
        var customer = string.IsNullOrWhiteSpace(user.GatewayCustomerId)
            ? await subscriberService.CreateStripeCustomer(user)
            : await subscriberService.GetCustomerOrThrow(user);

        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

        var sessionOptions = CreateSessionOptions(
            user,
            customer,
            premiumPlan,
            originatingAppVersion,
            originatingPlatform);

        var session = await stripeAdapter.CreateCheckoutSessionAsync(sessionOptions);

        return new PremiumCheckoutSessionResponseModel(session.Url);
    });

    /// <summary>
    /// Creates the options for creating a Stripe Checkout Session.
    /// </summary>
    /// <param name="customer"> The Stripe customer associated with the user. </param>
    /// <param name="premiumPlan"> The premium plan for which the Checkout Session is being created. </param>
    /// <param name="originatingAppVersion"> The version of the application initiating the Checkout Session. </param>
    /// <param name="originatingPlatform"> The platform (e.g., ios, android) from which the Checkout Session is initiated. </param>
    /// <returns> The created SessionCreateOptions for Stripe Checkout Session creation. </returns>
    private SessionCreateOptions CreateSessionOptions(
        User user,
        Customer customer,
        PremiumPlan premiumPlan,
        string originatingAppVersion,
        string originatingPlatform)
    {
        return new SessionCreateOptions
        {
            Customer = customer.Id,
            CustomerUpdate = new SessionCustomerUpdateOptions
            {
                Address = StripeConstants.CheckoutSession.CustomerUpdateAddressOptions.Auto,
            },
            Mode = StripeConstants.CheckoutSession.Modes.Subscription,
            LineItems =
            [
                new SessionLineItemOptions { Price = premiumPlan.Seat.StripePriceId, Quantity = 1 }
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    [StripeConstants.MetadataKeys.UserId] = user.Id.ToString(),
                    [StripeConstants.MetadataKeys.OriginatingPlatform] = originatingPlatform,
                    [StripeConstants.MetadataKeys.OriginatingAppVersion] = originatingAppVersion,
                }
            },
            SuccessUrl = globalSettings.Stripe.PremiumCheckoutSuccessUrl,
            CancelUrl = globalSettings.Stripe.PremiumCheckoutCancelUrl,
            AutomaticTax = new SessionAutomaticTaxOptions { Enabled = true },
            PaymentMethodTypes = [StripeConstants.PaymentMethodTypes.Card]
        };
    }
}
