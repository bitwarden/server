using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public static class InvitingUserOrganizationValidation
{
    public static ValidationResult<OrganizationDto> Validate(OrganizationDto organization)
    {

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            return new Invalid<OrganizationDto>(InviteUserValidationErrorMessages.NoPaymentMethodFoundError);
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            return new Invalid<OrganizationDto>(InviteUserValidationErrorMessages.NoSubscriptionFoundError);
        }

        return new Valid<OrganizationDto>(organization);
    }
}
