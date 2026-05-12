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
        var orgUsersByUserId = allOrgUsersForUsers
            .GroupBy(ou => ou.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Track which users are provider members.
        var providerUserIds = allProviderUsersForUsers
            .Select(pu => pu.UserId!.Value)
            .ToHashSet();

        var resultsByRequest = structuralResults.ToDictionary(r => r.Request);

        foreach (var request in validRequests)
        {
            var userId = request.OrganizationUser!.UserId!.Value;

            // Org must have the Automatic User Confirmation policy enabled
            if (!policyStatus.Enabled || request.Organization is not { UseAutomaticUserConfirmation: true })
            {
                resultsByRequest[request] = Invalid(request, new AutomaticallyConfirmUsersPolicyIsNotEnabled());
                continue;
            }

            // User must have 2FA enabled if the org's RequireTwoFactor policy is active
            var hasTwoFactor = isTwoFactorByUserId.GetValueOrDefault(userId);
            if (!hasTwoFactor)
            {
                var requireTwoFactor = requireTwoFactorByUserId.GetValueOrDefault(userId);

                if (requireTwoFactor?.IsTwoFactorRequiredForOrganization(orgId) == true)
                {
                    resultsByRequest[request] = Invalid(request, new UserDoesNotHaveTwoFactorEnabled());
                    continue;
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
                        resultsByRequest[request] = Invalid(request, new ProviderUsersCannotJoin());
                        continue;
                    }

                    // Users cannot belong to more than one organization.
                    var totalOrgMemberships = orgUsersByUserId.TryGetValue(userId, out var count) ? count : 0;
                    if (totalOrgMemberships > 1)
                    {
                        resultsByRequest[request] = Invalid(request, new UserCannotBelongToAnotherOrganization());
                        continue;
                    }
                }

                if (autoConfirmPolicy.IsEnabledForOrganizationsOtherThan(orgId))
                {
                    resultsByRequest[request] = Invalid(request, new OtherOrganizationDoesNotAllowOtherMembership());
                    continue;
                }
            }

            // All checks passed.
            resultsByRequest[request] = Valid(request);
        }

        // Return results in the original input order.
        return requestsList.Select(r => resultsByRequest[r]);
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
