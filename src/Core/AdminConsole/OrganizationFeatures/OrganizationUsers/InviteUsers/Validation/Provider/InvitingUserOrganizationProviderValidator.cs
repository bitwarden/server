using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Extensions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;

public static class InvitingUserOrganizationProviderValidator
{
    public static ValidationResult<InviteOrganizationProvider> Validate(InviteOrganizationProvider inviteOrganizationProvider)
    {
        if (inviteOrganizationProvider is not { Enabled: true })
        {
            return new Valid<InviteOrganizationProvider>(inviteOrganizationProvider);
        }

        if (inviteOrganizationProvider.IsBillable())
        {
            return new Invalid<InviteOrganizationProvider>(new ProviderBillableSeatLimitError(inviteOrganizationProvider));
        }

        if (inviteOrganizationProvider.Type == ProviderType.Reseller)
        {
            return new Invalid<InviteOrganizationProvider>(new ProviderResellerSeatLimitError(inviteOrganizationProvider));
        }

        return new Valid<InviteOrganizationProvider>(inviteOrganizationProvider);
    }
}
