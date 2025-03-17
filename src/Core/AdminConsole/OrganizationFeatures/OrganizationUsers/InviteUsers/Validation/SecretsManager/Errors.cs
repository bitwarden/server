using Bit.Core.AdminConsole.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;

public record OrganizationNoSecretsManagerError(SecretsManagerSubscriptionUpdate InvalidRequest)
    : Error<SecretsManagerSubscriptionUpdate>(Code, InvalidRequest)
{
    public const string Code = "Organization has no access to Secrets Manager";
}

public record SecretsManagerAdditionalSeatLimitReachedError(SecretsManagerSubscriptionUpdate InvalidRequest)
    : Error<SecretsManagerSubscriptionUpdate>(GetErrorMessage(InvalidRequest), InvalidRequest)
{
    public const string Code = "You have reached the maximum number of Secrets Manager seats ({0}) for this plan.";

    public static string GetErrorMessage(SecretsManagerSubscriptionUpdate invalidRequest) =>
        string.Format(Code,
            invalidRequest.SecretsManagerPlan.BaseSeats +
            invalidRequest.SecretsManagerPlan.MaxAdditionalSeats.GetValueOrDefault());
}

public record SecretsManagerSeatLimitReachedError(SecretsManagerSubscriptionUpdate InvalidRequest)
    : Error<SecretsManagerSubscriptionUpdate>(Code, InvalidRequest)
{
    public const string Code = "Secrets Manager seat limit has been reached.";
}

public record SecretsManagerCannotExceedPasswordManagerError(SecretsManagerSubscriptionUpdate InvalidRequest)
    : Error<SecretsManagerSubscriptionUpdate>(Code, InvalidRequest)
{
    public const string Code = "You cannot have more Secrets Manager seats than Password Manager seats.";
}
