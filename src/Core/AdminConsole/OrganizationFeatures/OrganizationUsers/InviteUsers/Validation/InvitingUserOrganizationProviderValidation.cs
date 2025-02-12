using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.Billing.Extensions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InvitingUserOrganizationProviderValidation
{
    public static ValidationResult<ProviderDto> Validate(ProviderDto provider)
    {
        if (provider is { Enabled: true })
        {
            if (provider.IsBillable())
            {
                return new Invalid<ProviderDto>(InviteUserValidationErrorMessages.ProviderBillableSeatLimitError);
            }

            if (provider.Type == ProviderType.Reseller)
            {
                return new Invalid<ProviderDto>(InviteUserValidationErrorMessages.ProviderResellerSeatLimitError);
            }
        }

        return new Valid<ProviderDto>(provider);
    }
}
