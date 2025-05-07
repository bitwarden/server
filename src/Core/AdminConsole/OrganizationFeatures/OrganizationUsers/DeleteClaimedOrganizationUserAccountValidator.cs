using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedUserAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class DeleteClaimedOrganizationUserAccountValidator(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository) : IDeleteClaimedOrganizationUserAccountValidator
{
    public async Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(List<DeleteUserValidationRequest> requests)
    {
        // The order of the validators matters.
        // Earlier validators assert nullable properties so that later validators don’t have to.
        var validators = new[]
        {
            EnsureUserBelongsToOrganization,
            EnsureUserStatusIsNotInvited,
            PreventSelfDeletion,
            EnsureUserIsClaimedByOrganization
        };

        var asyncValidators = new[]
        {
            EnsureOnlyOwnersCanDeleteOwnersAsync,
            EnsureUserIsNotSoleOrganizationOwnerAsync,
            EnsureUserIsNotSoleProviderOwnerAsync,
            EnsureCustomUsersCannotDeleteAdminsAsync
        };

        var validationResults = new List<ValidationResult<DeleteUserValidationRequest>>();
        foreach (var request in requests)
        {
            var result = await ExecuteValidatorsAsync(validators, asyncValidators, request);
            validationResults.Add(result);
        }

        return validationResults;
    }

    private static async Task<ValidationResult<DeleteUserValidationRequest>> ExecuteValidatorsAsync(
        Func<DeleteUserValidationRequest, ValidationResult<DeleteUserValidationRequest>>[] validators,
        Func<DeleteUserValidationRequest, Task<ValidationResult<DeleteUserValidationRequest>>>[] asyncValidators,
        DeleteUserValidationRequest request)
    {
        foreach (var validator in validators)
        {
            var result = validator(request);

            if (result is Invalid<DeleteUserValidationRequest>)
            {
                return result;
            }
        }

        foreach (var asyncValidator in asyncValidators)
        {
            var result = await asyncValidator(request);

            if (result is Invalid<DeleteUserValidationRequest>)
            {
                return result;
            }
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserBelongsToOrganization(DeleteUserValidationRequest request)
    {
        if (request.User == null || request.OrganizationUser == null)
        {
            return new Invalid<DeleteUserValidationRequest>(request, new UserNotFoundError());
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserIsClaimedByOrganization(DeleteUserValidationRequest request)
    {
        if (request.IsClaimed)
        {
            return new Valid<DeleteUserValidationRequest>(request);
        }
        return new Invalid<DeleteUserValidationRequest>(request, new UserNotClaimedError());
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserStatusIsNotInvited(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Status == OrganizationUserStatusType.Invited)
        {
            return new Invalid<DeleteUserValidationRequest>(request, new InvalidUserStatusError());
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private static ValidationResult<DeleteUserValidationRequest> PreventSelfDeletion(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.UserId == request.DeletingUserId)
        {
            return new Invalid<DeleteUserValidationRequest>(request, new CannotDeleteYourselfError());
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureOnlyOwnersCanDeleteOwnersAsync(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Type == OrganizationUserType.Owner! &&
            await currentContext.OrganizationOwner(request.OrganizationId))
        {
            return new Invalid<DeleteUserValidationRequest>(request, new CannotDeleteOwnersError());
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureUserIsNotSoleOrganizationOwnerAsync(DeleteUserValidationRequest request)
    {
        var onlyOwnerCount = await organizationUserRepository.GetCountByOnlyOwnerAsync(request.User!.Id);
        if (onlyOwnerCount > 0)
        {
            return new Invalid<DeleteUserValidationRequest>(request, new SoleOwnerError());
        }
        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureUserIsNotSoleProviderOwnerAsync(DeleteUserValidationRequest request)
    {
        var onlyOwnerProviderCount = await providerUserRepository.GetCountByOnlyOwnerAsync(request.User!.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return new Invalid<DeleteUserValidationRequest>(request, new SoleProviderError());
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureCustomUsersCannotDeleteAdminsAsync(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Type == OrganizationUserType.Admin && await currentContext.OrganizationCustom(request.OrganizationId))
        {
            return new Invalid<DeleteUserValidationRequest>(request, new CannotDeleteAdminsError());
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

}
