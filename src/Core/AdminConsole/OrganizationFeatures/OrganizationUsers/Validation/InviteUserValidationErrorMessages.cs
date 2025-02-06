namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public static class InviteUserValidationErrorMessages
{
    //
    public const string CannotAutoScaleOnSelfHostedError = "Cannot autoscale on self-hosted instance.";
    public const string SeatLimitHasBeenReachedError = "Seat limit has been reached.";
    public const string ProviderBillableSeatLimitError = "Seat limit has been reached. Please contact your provider to add more seats.";
    public const string ProviderResellerSeatLimitError = "Seat limit has been reached. Contact your provider to purchase additional seats.";
    public const string CancelledSubscriptionError = "Cannot autoscale with a canceled subscription.";
    public const string NoPaymentMethodFoundError = "No payment method found.";
    public const string NoSubscriptionFoundError = "No subscription found.";
}
