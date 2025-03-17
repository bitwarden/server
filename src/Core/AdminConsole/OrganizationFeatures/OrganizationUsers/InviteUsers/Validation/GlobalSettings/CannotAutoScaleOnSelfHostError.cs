using Bit.Core.AdminConsole.Errors;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;

public record CannotAutoScaleOnSelfHostError(IGlobalSettings InvalidSettings) : Error<IGlobalSettings>(Code, InvalidSettings)
{
    public const string Code = "CannotAutoScaleOnSelfHost";
}
