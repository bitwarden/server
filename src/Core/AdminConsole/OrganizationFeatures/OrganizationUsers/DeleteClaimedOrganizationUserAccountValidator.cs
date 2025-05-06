using System.Diagnostics;
using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class DeleteClaimedOrganizationUserAccountValidator(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository) : IDeleteClaimedOrganizationUserAccountValidator
{
    public async Task<PartialValidationResult<DeleteUserValidationRequest>> ValidateAsync(List<DeleteUserValidationRequest> requests)
    {
        var validResults = new List<DeleteUserValidationRequest>();
        var invalidResults = new List<Error<DeleteUserValidationRequest>>();

        foreach (var request in requests)
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

            var result = await ExecuteValidatorsAsync(validators, asyncValidators, request);

            switch (result)
            {
                case Valid<DeleteUserValidationRequest> valid:
                    validResults.Add(valid.Value);
                    break;
                case Invalid<DeleteUserValidationRequest> invalid:
                    invalidResults.Add(invalid.Error);
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        return new PartialValidationResult<DeleteUserValidationRequest>()
        {
            Invalid = invalidResults,
            Valid = validResults
        };
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
            return new Invalid<DeleteUserValidationRequest>(new RecordNotFoundError<DeleteUserValidationRequest>("Member not found.", request));
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserIsClaimedByOrganization(DeleteUserValidationRequest request)
    {
        if (request.IsClaimed)
        {
            return new Valid<DeleteUserValidationRequest>(request);
        }
        return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("Member is not managed by the organization.", request));
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserStatusIsNotInvited(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Status == OrganizationUserStatusType.Invited)
        {
            return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("You cannot delete a member with Invited status.", request));
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private static ValidationResult<DeleteUserValidationRequest> PreventSelfDeletion(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.UserId == request.DeletingUserId)
        {
            return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("You cannot delete yourself.", request));
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureOnlyOwnersCanDeleteOwnersAsync(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Type != OrganizationUserType.Owner)
        {
            return new Valid<DeleteUserValidationRequest>(request);
        }

        if (!await currentContext.OrganizationOwner(request.OrganizationId))
        {
            return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("Only owners can delete other owners.", request));
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureUserIsNotSoleOrganizationOwnerAsync(DeleteUserValidationRequest request)
    {
        var onlyOwnerCount = await organizationUserRepository.GetCountByOnlyOwnerAsync(request.User!.Id);
        if (onlyOwnerCount > 0)
        {
            return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.", request));
        }
        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureUserIsNotSoleProviderOwnerAsync(DeleteUserValidationRequest request)
    {
        var onlyOwnerProviderCount = await providerUserRepository.GetCountByOnlyOwnerAsync(request.User!.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("Cannot delete this user because it is the sole owner of at least one provider. Please delete these providers or upgrade another user.", request));
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureCustomUsersCannotDeleteAdminsAsync(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Type == OrganizationUserType.Admin && await currentContext.OrganizationCustom(request.OrganizationId))
        {
            return new Invalid<DeleteUserValidationRequest>(new BadRequestError<DeleteUserValidationRequest>("Custom users can not delete admins.", request));
        }

        return new Valid<DeleteUserValidationRequest>(request);
    }

}
