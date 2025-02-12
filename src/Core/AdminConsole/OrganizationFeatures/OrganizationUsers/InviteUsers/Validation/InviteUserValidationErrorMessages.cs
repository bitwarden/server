﻿namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InviteUserValidationErrorMessages
{
    public const string CannotAutoScaleOnSelfHostedError = "Cannot autoscale on self-hosted instance.";
    public const string SeatLimitHasBeenReachedError = "Seat limit has been reached.";
    public const string ProviderBillableSeatLimitError = "Seat limit has been reached. Please contact your provider to add more seats.";
    public const string ProviderResellerSeatLimitError = "Seat limit has been reached. Contact your provider to purchase additional seats.";
    public const string CancelledSubscriptionError = "Cannot autoscale with a canceled subscription.";
    public const string NoPaymentMethodFoundError = "No payment method found.";
    public const string NoSubscriptionFoundError = "No subscription found.";

    // Secrets Manager Invite Users Error Messages
    public const string OrganizationNoSecretsManager = "Organization has no access to Secrets Manager";
    public const string SecretsManagerSeatLimitReached = "Secrets Manager seat limit has been reached.";
    public const string SecretsManagerCannotExceedPasswordManager = "You cannot have more Secrets Manager seats than Password Manager seats.";
}
