using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Commands;

public interface ICancelSubscriptionCommand
{
    /// <summary>
    /// Cancels a user or organization's subscription while including user-provided feedback.
    /// If the user's <see cref="User.PremiumExpirationDate"/> or organization's <see cref="Organization.ExpirationDate"/> has not yet been reached,
    /// this command sets the subscription's <b>"cancel_at_end_of_period"</b> property to <see langword="true"/>.
    /// Otherwise, this command cancels the subscription immediately.
    /// </summary>
    /// <param name="subscriber">The <see cref="User"/> or <see cref="Organization"/> with the subscription to cancel.</param>
    /// <param name="offboardingSurveyResponse">An <see cref="OffboardingSurveyResponse"/> DTO containing user-provided feedback on why they are cancelling the subscription.</param>
    Task CancelSubscription(
        ISubscriber subscriber,
        OffboardingSurveyResponse offboardingSurveyResponse);
}
