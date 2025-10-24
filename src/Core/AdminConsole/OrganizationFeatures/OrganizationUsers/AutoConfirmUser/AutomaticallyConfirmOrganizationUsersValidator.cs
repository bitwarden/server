using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class AutomaticallyConfirmOrganizationUsersValidator : IAutomaticallyConfirmOrganizationUsersValidator
{
    public Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> ValidateAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {


        // joining user must be accepted
        // joining user match org
        // joining user must have user record

        // joining user cannot be the owner or admin of another free org if org being added to is free

        // check that the joining user conforms to the organization they're joining
        // use policy reqs if enabled (see ConfirmOrgUserCommand)
        // else use policy service

        // this includes single org and two factor - maybe more

        return Task.FromResult(Invalid(request, new InvalidUserStatusError()));
    }
}
