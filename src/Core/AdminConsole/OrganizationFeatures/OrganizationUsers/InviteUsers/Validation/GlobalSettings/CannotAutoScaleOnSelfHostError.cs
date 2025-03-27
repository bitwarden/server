using Bit.Core.AdminConsole.Errors;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public record CannotAutoScaleOnSelfHostError(EnvironmentRequest Invalid) : Error<EnvironmentRequest>(Code, Invalid)
{
    public const string Code = "Cannot auto scale self-host.";
}
