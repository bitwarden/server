using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Billing.Extensions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;

public static class InvitingUserOrganizationProviderValidation
{
    public static ValidationResult<ProviderDto> Validate(ProviderDto provider)
    {
        if (provider is { Enabled: true })
        {
            if (provider.IsBillable())
            {
                return new Invalid<ProviderDto>(new ProviderBillableSeatLimitError(provider));
            }

            if (provider.Type == ProviderType.Reseller)
            {
                return new Invalid<ProviderDto>(new ProviderResellerSeatLimitError(provider));
            }
        }

        return new Valid<ProviderDto>(provider);
    }
}
