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
        var tasks = requests.Select(ValidateAsync);
        var results = await Task.WhenAll(tasks);
        return results;
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> ValidateAsync(DeleteUserValidationRequest request)
    {
        // Ensure user exists
        if (request.User == null || request.OrganizationUser == null)
        {
            return (request, new UserNotFoundError());
        }

        // Cannot delete invited users
        if (request.OrganizationUser.Status == OrganizationUserStatusType.Invited)
        {
            return (request, new InvalidUserStatusError());
        }

        // Cannot delete yourself
        if (request.OrganizationUser.UserId == request.DeletingUserId)
        {
            return (request, new CannotDeleteYourselfError());
        }

        // Can only delete a claimed user
        if (!request.IsClaimed)
        {
            return (request, new UserNotClaimedError());
        }

        // Cannot delete an owner unless you are an owner or provider
        if (request.OrganizationUser.Type == OrganizationUserType.Owner &&
            await currentContext.OrganizationOwner(request.OrganizationId))
        {
            return (request, new CannotDeleteOwnersError());
        }

        // Cannot delete a user who is the sole owner of an organization
        var onlyOwnerCount = await organizationUserRepository.GetCountByOnlyOwnerAsync(request.User.Id);
        if (onlyOwnerCount > 0)
        {
            return (request, new SoleOwnerError());
        }

        // Cannot delete a user who is the sole member of a provider
        var onlyOwnerProviderCount = await providerUserRepository.GetCountByOnlyOwnerAsync(request.User.Id);
        if (onlyOwnerProviderCount > 0)
        {
            return (request, new SoleProviderError());
        }

        // Custom users cannot delete admins
        if (request.OrganizationUser.Type == OrganizationUserType.Admin && await currentContext.OrganizationCustom(request.OrganizationId))
        {
            return (request, new CannotDeleteAdminsError());
        }

        return request;
    }
}
