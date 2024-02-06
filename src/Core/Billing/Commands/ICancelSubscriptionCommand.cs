using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Stripe;

namespace Bit.Core.Billing.Commands;

public interface ICancelSubscriptionCommand
{
    /// <summary>
    /// Cancels a user or organization's subscription while including user-provided feedback via the <paramref name="offboardingSurveyResponse"/>.
    /// If the <paramref name="cancelImmediately"/> flag is <see langword="false"/>,
    /// this command sets the subscription's <b>"cancel_at_end_of_period"</b> property to <see langword="true"/>.
    /// Otherwise, this command cancels the subscription immediately.
    /// </summary>
    /// <param name="subscription">The <see cref="User"/> or <see cref="Organization"/> with the subscription to cancel.</param>
    /// <param name="offboardingSurveyResponse">An <see cref="OffboardingSurveyResponse"/> DTO containing user-provided feedback on why they are cancelling the subscription.</param>
    /// <param name="cancelImmediately">A flag indicating whether to cancel the subscription immediately or at the end of the subscription period.</param>
    /// <exception cref="GatewayException">Thrown when the provided subscription is already in an inactive state.</exception>
    Task CancelSubscription(
        Subscription subscription,
        OffboardingSurveyResponse offboardingSurveyResponse,
        bool cancelImmediately);
}
