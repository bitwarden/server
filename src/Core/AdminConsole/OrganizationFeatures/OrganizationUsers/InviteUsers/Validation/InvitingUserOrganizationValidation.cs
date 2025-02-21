using Bit.Core.AdminConsole.Models.Business;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class InvitingUserOrganizationValidation
{
    public static ValidationResult<OrganizationDto> Validate(OrganizationDto organization)
    {
        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            return new Invalid<OrganizationDto>(NoPaymentMethodFoundError);
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            return new Invalid<OrganizationDto>(NoSubscriptionFoundError);
        }

        return new Valid<OrganizationDto>(organization);
    }
}
