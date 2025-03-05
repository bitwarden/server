using Bit.Core.AdminConsole.Models.Business;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InvitingUserOrganizationValidation
{
    public static ValidationResult<InviteOrganization> Validate(InviteOrganization inviteOrganization)
    {
        if (inviteOrganization.Seats is null)
        {
            return new Valid<InviteOrganization>(inviteOrganization);
        }

        if (string.IsNullOrWhiteSpace(inviteOrganization.GatewayCustomerId))
        {
            return new Invalid<InviteOrganization>(NoPaymentMethodFoundError);
        }

        if (string.IsNullOrWhiteSpace(inviteOrganization.GatewaySubscriptionId))
        {
            return new Invalid<InviteOrganization>(NoSubscriptionFoundError);
        }

        return new Valid<InviteOrganization>(inviteOrganization);
    }
}
