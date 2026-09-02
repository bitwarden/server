using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

public class DeleteClaimedOrganizationUserAccountValidator(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository) : IDeleteClaimedOrganizationUserAccountValidator
{
    public async Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var tasks = requests.Select(ValidateAsync);
        var results = await Task.WhenAll(tasks);
        return results;
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> ValidateAsync(DeleteUserValidationRequest request)
    {
        // Ensure user exists
        if (request.User == null || request.OrganizationUser == null)
        {
            return Invalid(request, new UserNotFoundError());
        }

        // Cannot delete invited users
        if (request.OrganizationUser.Status == OrganizationUserStatusType.Invited)
        {
            return Invalid(request, new InvalidUserStatusError());
        }

        // Cannot delete yourself
        if (request.OrganizationUser.UserId == request.DeletingUserId)
        {
            return Invalid(request, new CannotDeleteYourselfError());
        }

        // Can only delete a claimed user
        if (!request.IsClaimed)
        {
            return Invalid(request, new UserNotClaimedError());
        }

        // Cannot delete an owner unless you are an owner or provider
        if (request.OrganizationUser.Type == OrganizationUserType.Owner &&
            !await currentContext.OrganizationOwner(request.OrganizationId))
        {
            return Invalid(request, new CannotDeleteOwnersError());
        }

        // Cannot delete a user who is the sole owner of an organization
        var onlyOwnerCount = await organizationUserRepository.GetCountByOnlyOwnerAsync(request.User.Id);
        if (onlyOwnerCount > 0)
        {
            return Invalid(request, new SoleOwnerError());
        }

        // Cannot delete a user who is the sole member of a provider
        var onlyOwnerProviderCount = await providerUserRepository.GetCountByOnlyOwnerAsync(request.User.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return Invalid(request, new SoleProviderError());
        }

        // Custom users cannot delete admins
        if (request.OrganizationUser.Type == OrganizationUserType.Admin && await currentContext.OrganizationCustom(request.OrganizationId))
        {
            return Invalid(request, new CannotDeleteAdminsError());
        }

        return Valid(request);
    }
}
