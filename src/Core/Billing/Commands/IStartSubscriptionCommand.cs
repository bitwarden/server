using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Commands;

public interface IStartSubscriptionCommand
{
    /// <summary>
    /// Starts a Stripe <see cref="Stripe.Subscription"/> for the given <paramref name="provider"/> utilizing the provided
    /// <paramref name="taxInfo"/> to handle automatic taxation and non-US tax identification. <see cref="Provider"/> subscriptions
    /// will always be started with a <see cref="Stripe.SubscriptionItem"/> for both the <see cref="PlanType.TeamsMonthly"/> and <see cref="PlanType.EnterpriseMonthly"/>
    /// plan, and the quantity for each item will be equal the provider's seat minimum for each respective plan.
    /// </summary>
    /// <param name="provider">The provider to create the <see cref="Stripe.Subscription"/> for.</param>
    /// <param name="taxInfo">The tax information to use for automatic taxation and non-US tax identification.</param>
    Task StartSubscription(
        Provider provider,
        TaxInfo taxInfo);
}
