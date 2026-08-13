using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

public class DeleteClaimedOrganizationUserAccountValidator(
    ICurrentContext currentContext,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository,
    IOrganizationUserValidationService organizationUserValidationService) : IDeleteClaimedOrganizationUserAccountValidator
{
    public async Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(IEnumerable<DeleteUserValidationRequest> requests)
    {
        var requestList = requests.ToList();
        var manageErrorsByOrgUserId = await GetManageErrorsAsync(requestList);

        var tasks = requestList.Select(request => ValidateAsync(request, manageErrorsByOrgUserId));
        var results = await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Resolves the "can the acting user manage this target's role" decision once for the whole batch via
    /// <see cref="IOrganizationUserValidationService"/>. All requests in a batch share the same acting user and
    /// organization, so the acting user's role/provider-status is only looked up once.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Error?>> GetManageErrorsAsync(ICollection<DeleteUserValidationRequest> requests)
    {
        var targetsById = requests
            .Where(r => r.OrganizationUser is not null)
            .ToDictionary(r => r.OrganizationUserId, r => (IOrganizationUserRole)r.OrganizationUser!);

        if (targetsById.Count == 0)
        {
            return new Dictionary<Guid, Error?>();
        }

        var first = requests.First(r => r.OrganizationUser is not null);
        var actingOrganization = currentContext.GetOrganization(first.OrganizationId);
        var actingUser = actingOrganization is null
            ? null
            : new OrganizationUserRole(actingOrganization.Type, first.OrganizationId, actingOrganization.Permissions);

        return await organizationUserValidationService.CanManageAsync(
            first.DeletingUserId, actingUser, first.OrganizationId, targetsById) ?? new Dictionary<Guid, Error?>();
    }

    private async Task<ValidationResult<DeleteUserValidationRequest>> ValidateAsync(
        DeleteUserValidationRequest request, IReadOnlyDictionary<Guid, Error?> manageErrorsByOrgUserId)
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

        // Cannot delete a member whose role you cannot manage (e.g. an owner, or an admin if you are a custom user)
        if (manageErrorsByOrgUserId.TryGetValue(request.OrganizationUserId, out var manageError) && manageError is not null)
        {
            return Invalid(request, manageError);
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

        return Valid(request);
    }
}
