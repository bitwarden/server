using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using OneOf;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

// public abstract record ValidationResult<T>(T Request)
// {
//     public static implicit operator ValidationResult<T>(T request) => new Valid<T>(request);
//     public static implicit operator ValidationResult<T>(ValueTuple<T, Error> requestWithError) => new Invalid<T>(requestWithError.Item1, requestWithError.Item2);
//
//     public bool Valid => this is Valid<T>;
//     public bool Invalid => this is Invalid<T>;
//
//     public TResult Match<TResult>(Func<Valid<T>, TResult> validFunc, Func<Invalid<T>, TResult> invalidFunc)
//         => this switch
//             {
//                 Invalid<T> => invalidFunc((Invalid<T>)this),
//                 Valid<T> => validFunc((Valid<T>)this),
//                 _ => throw new Exception()
//             };
// }
// public record Valid<T>(T Request) : ValidationResult<T>(Request);
// public record Invalid<T>(T Request, Error Error) : ValidationResult<T>(Request);

// public static class ValidationResultExtensions
// {
//     public static List<Valid<T>> ValidResults<T>(this IEnumerable<ValidationResult<T>> results) =>
//         results.OfType<Valid<T>>().ToList();
// }

public record Valid<T>(T Request);
public record Invalid<T>(T Request, Error Error);
public class ValidationResult<T> : OneOfBase<Valid<T>, Invalid<T>>
{
    private ValidationResult(OneOf<Valid<T>, Invalid<T>> _) : base(_) {}
    public static implicit operator ValidationResult<T>(T request) => new (new Valid<T>(request));
    public static implicit operator ValidationResult<T>(ValueTuple<T, Error> requestWithError) => new (new Invalid<T>(requestWithError.Item1, requestWithError.Item2));

    public bool Valid => IsT0;
    public bool Invalid => IsT1;
}
public static class ValidationResultExtensions
{
    public static List<Valid<T>> ValidResults<T>(this IEnumerable<ValidationResult<T>> results) =>
        results
            .Where(r => r.IsT0)
            .Select(r => r.AsT0)
            .ToList();
}

public record Error(string Message);

public class DeleteClaimedOrganizationUserAccountValidator(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository) : IDeleteClaimedOrganizationUserAccountValidator
{
    public async Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(IEnumerable<DeleteUserValidationRequest> requests)
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
            if (result.Invalid)
            {
                return result;
            }
        }

        foreach (var asyncValidator in asyncValidators)
        {
            var result = await asyncValidator(request);
            if (result.Invalid)
            {
                return result;
            }
        }

        return request;
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserBelongsToOrganization(DeleteUserValidationRequest request)
    {
        if (request.User == null || request.OrganizationUser == null)
        {
            return (request, new UserNotFoundError());
        }

        return request;
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserIsClaimedByOrganization(DeleteUserValidationRequest request)
    {
        if (request.IsClaimed)
        {
            return request;
        }
        return (request, new UserNotClaimedError());
    }

    private static ValidationResult<DeleteUserValidationRequest> EnsureUserStatusIsNotInvited(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Status == OrganizationUserStatusType.Invited)
        {
            return (request, new InvalidUserStatusError());
        }

        return request;
    }

    private static ValidationResult<DeleteUserValidationRequest> PreventSelfDeletion(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.UserId == request.DeletingUserId)
        {
            return (request, new CannotDeleteYourselfError());
        }

        return request;
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureOnlyOwnersCanDeleteOwnersAsync(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Type == OrganizationUserType.Owner! &&
            await currentContext.OrganizationOwner(request.OrganizationId))
        {
            return (request, new CannotDeleteOwnersError());
        }

        return request;
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureUserIsNotSoleOrganizationOwnerAsync(DeleteUserValidationRequest request)
    {
        var onlyOwnerCount = await organizationUserRepository.GetCountByOnlyOwnerAsync(request.User!.Id);
        if (onlyOwnerCount > 0)
        {
            return (request, new SoleOwnerError());
        }
        return request;
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureUserIsNotSoleProviderOwnerAsync(DeleteUserValidationRequest request)
    {
        var onlyOwnerProviderCount = await providerUserRepository.GetCountByOnlyOwnerAsync(request.User!.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return (request, new SoleProviderError());
        }

        return request;
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> EnsureCustomUsersCannotDeleteAdminsAsync(DeleteUserValidationRequest request)
    {
        if (request.OrganizationUser!.Type == OrganizationUserType.Admin && await currentContext.OrganizationCustom(request.OrganizationId))
        {
            return (request, new CannotDeleteAdminsError());
        }

        return request;
    }

}
