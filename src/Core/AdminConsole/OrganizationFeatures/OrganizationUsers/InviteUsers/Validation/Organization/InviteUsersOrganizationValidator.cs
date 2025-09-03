using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;

public interface IInviteUsersOrganizationValidator : IValidator<InviteOrganization>;

public class InviteUsersOrganizationValidator : IInviteUsersOrganizationValidator
{
    public Task<ValidationResult<InviteOrganization>> ValidateAsync(InviteOrganization inviteOrganization)
    {
        if (inviteOrganization.Seats is null)
        {
            return Task.FromResult<ValidationResult<InviteOrganization>>(
                new Valid<InviteOrganization>(inviteOrganization));
        }

        if (string.IsNullOrWhiteSpace(inviteOrganization.GatewayCustomerId))
        {
            return Task.FromResult<ValidationResult<InviteOrganization>>(
                new Invalid<InviteOrganization>(new OrganizationNoPaymentMethodFoundError(inviteOrganization)));
        }

        if (string.IsNullOrWhiteSpace(inviteOrganization.GatewaySubscriptionId))
        {
            return Task.FromResult<ValidationResult<InviteOrganization>>(
                new Invalid<InviteOrganization>(new OrganizationNoSubscriptionFoundError(inviteOrganization)));
        }

        return Task.FromResult<ValidationResult<InviteOrganization>>(new Valid<InviteOrganization>(inviteOrganization));
    }
}
