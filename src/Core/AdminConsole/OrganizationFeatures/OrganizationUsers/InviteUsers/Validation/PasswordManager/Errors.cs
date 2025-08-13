using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;

public record PasswordManagerSeatLimitHasBeenReachedError(PasswordManagerSubscriptionUpdate InvalidRequest)
    : Error<PasswordManagerSubscriptionUpdate>(Code, InvalidRequest)
{
    public const string Code = "Seat limit has been reached.";
}

public record PasswordManagerPlanDoesNotAllowAdditionalSeatsError(PasswordManagerSubscriptionUpdate InvalidRequest)
    : Error<PasswordManagerSubscriptionUpdate>(Code, InvalidRequest)
{
    public const string Code = "Plan does not allow additional seats.";
}

public record PasswordManagerPlanOnlyAllowsMaxAdditionalSeatsError(PasswordManagerSubscriptionUpdate InvalidRequest)
    : Error<PasswordManagerSubscriptionUpdate>(GetErrorMessage(InvalidRequest), InvalidRequest)
{
    private static string GetErrorMessage(PasswordManagerSubscriptionUpdate invalidRequest) =>
        string.Format(Code, invalidRequest.PasswordManagerPlan.MaxAdditionalSeats);

    public const string Code = "Organization plan allows a maximum of {0} additional seats.";
}

public record PasswordManagerMustHaveSeatsError(PasswordManagerSubscriptionUpdate InvalidRequest)
    : Error<PasswordManagerSubscriptionUpdate>(Code, InvalidRequest)
{
    public const string Code = "You do not have any Password Manager seats!";
}
