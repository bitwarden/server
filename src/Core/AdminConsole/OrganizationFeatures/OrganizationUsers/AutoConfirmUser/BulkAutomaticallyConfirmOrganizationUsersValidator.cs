using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class BulkAutomaticallyConfirmOrganizationUsersValidator(
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyRequirementQuery policyRequirementQuery,
    IPolicyQuery policyQuery,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository) : IBulkAutomaticallyConfirmOrganizationUsersValidator
{
    public async Task<IEnumerable<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>> ValidateManyAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests)
    {
        var requestsList = requests.ToList();

        if (requestsList.Count == 0)
        {
            return [];
        }

        // Quick structural checks before making any DB calls.
        // We bail out early per-request so we only fetch bulk data for structurally valid users.
        var structuralResults = requestsList
            .Select(ValidateStructure)
            .ToList();

        var validRequests = structuralResults
            .Where(r => r.IsValid)
            .Select(r => r.Request)
            .ToList();

        if (validRequests.Count == 0)
        {
            return structuralResults;
        }

        // All valid requests are for the same organization — verified by the bulk command before
        // calling here. OrganizationId is derived from the hydrated Organization object.
        var orgId = validRequests[0].OrganizationId;

        var userIds = validRequests
            .Select(r => r.OrganizationUser!.UserId!.Value)
            .Distinct()
            .ToList();

        // Fetch all data dependencies in parallel, once for the entire batch.
        var (
            policyStatus,
            twoFactorResults,
            requireTwoFactorResults,
            autoConfirmPolicyResults,
            allOrgUsersForUsers,
            allProviderUsersForUsers
        ) = await FetchBulkDataAsync(orgId, userIds);

        var isTwoFactorByUserId = twoFactorResults.ToDictionary(r => r.userId, r => r.twoFactorIsEnabled);
        var requireTwoFactorByUserId = requireTwoFactorResults.ToDictionary(r => r.UserId, r => r.Requirement);
        var autoConfirmPolicyByUserId = autoConfirmPolicyResults.ToDictionary(r => r.UserId, r => r.Requirement);

        // Group all org memberships by the user so we can do the multi-org check in memory.
        // GetManyByManyUsersAsync queries by UserId only (WHERE UserId IN (...)), matching the
        // single-user path's GetManyByUserAsync. Neither returns Invited rows with a null UserId.
        // The || Email fallback in AutomaticUserConfirmationPolicyEnforcementValidator is only
        // used to locate the current org-user row defensively and does not extend the data fetched here.
        var orgUserCountByUserId = allOrgUsersForUsers
            .GroupBy(ou => ou.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Track which users are provider members.
        var providerUserIds = allProviderUsersForUsers
            .Select(pu => pu.UserId!.Value)
            .ToHashSet();

        // Org-level check: the policy must be enabled for the organization. Since all valid requests
        // share the same organization, this either passes or fails for the entire batch.
        if (!policyStatus.Enabled || validRequests[0].Organization is not { UseAutomaticUserConfirmation: true })
        {
            return structuralResults.Select(r => r.IsValid
                ? Invalid(r.Request, new AutomaticallyConfirmUsersPolicyIsNotEnabled())
                : r);
        }

        // Per-user validation using bulk-fetched data — pure mapping, no side effects.
        var bulkResultByRequest = validRequests
            .Select(r => ValidateRequest(r, orgId, isTwoFactorByUserId, requireTwoFactorByUserId,
                autoConfirmPolicyByUserId, providerUserIds, orgUserCountByUserId))
            .ToDictionary(r => r.Request);

        // structuralResults is already in input order; replace valid entries with their bulk result.
        return structuralResults.Select(r =>
            bulkResultByRequest.TryGetValue(r.Request, out var bulk) ? bulk : r);
    }

    /// <summary>
    /// Validates a single request against the bulk-fetched data for the organization.
    /// All parameters are pre-computed dictionaries/sets to avoid per-user DB calls.
    /// </summary>
    private static ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest> ValidateRequest(
        AutomaticallyConfirmOrganizationUserValidationRequest request,
        Guid orgId,
        Dictionary<Guid, bool> isTwoFactorByUserId,
        Dictionary<Guid, RequireTwoFactorPolicyRequirement> requireTwoFactorByUserId,
        Dictionary<Guid, AutomaticUserConfirmationPolicyRequirement> autoConfirmPolicyByUserId,
        HashSet<Guid> providerUserIds,
        Dictionary<Guid, int> orgUserCountByUserId)
    {
        var userId = request.OrganizationUser!.UserId!.Value;

        // User must have 2FA enabled if the org's RequireTwoFactor policy is active.
        if (!isTwoFactorByUserId.GetValueOrDefault(userId))
        {
            var requireTwoFactor = requireTwoFactorByUserId.GetValueOrDefault(userId);
            if (requireTwoFactor?.IsTwoFactorRequiredForOrganization(orgId) == true)
            {
                return Invalid(request, new UserDoesNotHaveTwoFactorEnabled());
            }
        }

        // Enforce the Automatic User Confirmation cross-org policy in-memory using bulk-fetched data.
        var autoConfirmPolicy = autoConfirmPolicyByUserId.GetValueOrDefault(userId);

        if (autoConfirmPolicy is not null)
        {
            if (autoConfirmPolicy.IsEnabled(orgId))
            {
                // Provider users cannot be confirmed into an auto-confirm org.
                if (providerUserIds.Contains(userId))
                {
                    return Invalid(request, new ProviderUsersCannotJoin());
                }

                // Users cannot belong to more than one organization.
                var totalOrgMemberships = orgUserCountByUserId.TryGetValue(userId, out var count) ? count : 0;
                if (totalOrgMemberships > 1)
                {
                    return Invalid(request, new UserCannotBelongToAnotherOrganization());
                }
            }

            if (autoConfirmPolicy.IsEnabledForOrganizationsOtherThan(orgId))
            {
                return Invalid(request, new OtherOrganizationDoesNotAllowOtherMembership());
            }
        }

        return Valid(request);
    }

    /// <summary>
    /// Validates structural preconditions that don't require any DB calls.
    /// The caller (bulk command) is responsible for ensuring OrganizationUser is non-null before
    /// passing requests here.
    /// </summary>
    private static ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest> ValidateStructure(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        if (request.OrganizationUser!.UserId is null)
        {
            return Invalid(request, new UserNotFoundError());
        }

        if (request.Organization is null)
        {
            return Invalid(request, new OrganizationNotFound());
        }

        if (request.OrganizationUser.OrganizationId != request.Organization.Id)
        {
            return Invalid(request, new OrganizationUserIdIsInvalid());
        }

        if (request.OrganizationUser.Status != OrganizationUserStatusType.Accepted)
        {
            return Invalid(request, new UserIsNotAccepted());
        }

        if (request.OrganizationUser.Type != OrganizationUserType.User)
        {
            return Invalid(request, new UserIsNotUserType());
        }

        return Valid(request);
    }

    private async Task<(
        PolicyStatus policyStatus,
        IEnumerable<(Guid userId, bool twoFactorIsEnabled)> twoFactorResults,
        IEnumerable<(Guid UserId, RequireTwoFactorPolicyRequirement Requirement)> requireTwoFactorResults,
        IEnumerable<(Guid UserId, AutomaticUserConfirmationPolicyRequirement Requirement)> autoConfirmPolicyResults,
        ICollection<OrganizationUser> allOrgUsersForUsers,
        ICollection<ProviderUser> allProviderUsersForUsers
    )> FetchBulkDataAsync(Guid orgId, IReadOnlyCollection<Guid> userIds)
    {
        var policyTask = policyQuery.RunAsync(orgId, PolicyType.AutomaticUserConfirmation);
        var twoFactorTask = twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(userIds);
        var requireTwoFactorTask = policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(userIds);
        var autoConfirmTask = policyRequirementQuery.GetAsync<AutomaticUserConfirmationPolicyRequirement>(userIds);
        var orgUsersTask = organizationUserRepository.GetManyByManyUsersAsync(userIds);
        var providerUsersTask = providerUserRepository.GetManyByManyUsersAsync(userIds);

        await Task.WhenAll(policyTask, twoFactorTask, requireTwoFactorTask, autoConfirmTask, orgUsersTask, providerUsersTask);

        return (
            policyTask.Result,
            twoFactorTask.Result,
            requireTwoFactorTask.Result,
            autoConfirmTask.Result,
            orgUsersTask.Result,
            providerUsersTask.Result
        );
    }
}
