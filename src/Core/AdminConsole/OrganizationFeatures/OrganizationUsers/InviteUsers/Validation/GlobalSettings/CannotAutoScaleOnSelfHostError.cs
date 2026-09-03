using Bit.Core.AdminConsole.Utilities.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public record CannotAutoScaleOnSelfHostError(EnvironmentRequest Invalid) : Error<EnvironmentRequest>(GetErrorMessage(Invalid), Invalid)
{
    private static string GetErrorMessage(EnvironmentRequest invalid) =>
        string.Format(Code, invalid.PasswordManagerSubscriptionUpdate.Seats);

    public const string Code = "Seat limit of {0} has been reached. Update your organization license to invite more members.";
}
