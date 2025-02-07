using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public static class InvitingUserOrganizationValidation
{
    public static async Task<ValidationResult<OrganizationDto>> Validate(OrganizationValidationDto organizationDto)
    {
        var (organization, paymentService) = (organizationDto.Organization, organizationDto.PaymentService);

        if (organization.Plan is { ProductTier: ProductTierType.Free })
        {
            return new Valid<OrganizationDto>(organization);
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            return new Invalid<OrganizationDto>(InviteUserValidationErrorMessages.NoPaymentMethodFoundError);
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            return new Invalid<OrganizationDto>(InviteUserValidationErrorMessages.NoSubscriptionFoundError);
        }

        var paymentSubscription = await paymentService.GetSubscriptionAsync(organization);

        if (InviteUserPaymentValidation.Validate(PaymentSubscriptionDto.FromSubscriptionInfo(paymentSubscription)) is
            Invalid<PaymentSubscriptionDto> invalidPaymentValidation)
        {
            return new Invalid<OrganizationDto>(invalidPaymentValidation.ErrorMessageString);
        }

        return new Valid<OrganizationDto>(organization);
    }
}

public class OrganizationValidationDto
{
    public OrganizationDto Organization { get; init; }
    public IPaymentService PaymentService { get; init; }
}
