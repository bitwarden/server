using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;

public interface IInviteUserOrganizationValidator : IValidator<InviteOrganization>;

public class InviteUserOrganizationValidator : IInviteUserOrganizationValidator
{
    public async Task<ValidationResult<InviteOrganization>> ValidateAsync(InviteOrganization inviteOrganization)
    {
        if (inviteOrganization.Seats is null)
        {
            return new Valid<InviteOrganization>(inviteOrganization);
        }

        if (string.IsNullOrWhiteSpace(inviteOrganization.GatewayCustomerId))
        {
            return new Invalid<InviteOrganization>(new OrganizationNoPaymentMethodFoundError(inviteOrganization));
        }

        if (string.IsNullOrWhiteSpace(inviteOrganization.GatewaySubscriptionId))
        {
            return new Invalid<InviteOrganization>(new OrganizationNoSubscriptionFoundError(inviteOrganization));
        }

        return new Valid<InviteOrganization>(inviteOrganization);
    }
}
