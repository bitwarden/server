using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class BulkAutomaticallyConfirmOrganizationUsersValidator(
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyRequirementQuery policyRequirementQuery,
    IPolicyQuery policyQuery,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository,
    IAutomaticUserConfirmationPolicyEnforcementValidator autoConfirmPolicyEnforcementValidator) : IBulkAutomaticallyConfirmOrganizationUsersValidator
{
    public async Task<IEnumerable<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>> ValidateManyAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests,
        Organization organization)
    {
        var orgId = organization.Id;
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

        var userIds = validRequests
            .Select(r => r.OrganizationUser!.UserId!.Value)
            .Distinct()
            .ToList();

        // Fetch all data dependencies in parallel, once for the entire batch.
        var (
            policyStatus,
            isTwoFactorEnabledByUserId,
            requireTwoFactorByUserId,
            autoConfirmPolicyByUserId,
            orgUserCountByUserId,
            providerUserIds
        ) = await FetchBulkDataAsync(orgId, userIds);

        // Org-level check: the policy must be enabled for the organization.
        // This either passes or fails for the entire batch.
        if (!policyStatus.Enabled || !organization.UseAutomaticUserConfirmation)
        {
            return structuralResults.Select(r => r.IsValid
                ? Invalid(r.Request, new AutomaticallyConfirmUsersPolicyIsNotEnabled())
                : r);
        }

        // Per-user validation using bulk-fetched data — pure mapping, no side effects.
        var bulkResults = validRequests
            .Select(r => ValidateRequest(r, orgId, isTwoFactorEnabledByUserId, requireTwoFactorByUserId,
                autoConfirmPolicyByUserId, providerUserIds, orgUserCountByUserId));

        return bulkResults.Concat(structuralResults.Where(r => r.IsError));
    }

    /// <summary>
    /// Validates a single request against the bulk-fetched data for the organization.
    /// All parameters are pre-computed dictionaries/sets to avoid per-user DB calls.
    /// </summary>
    private ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest> ValidateRequest(
        AutomaticallyConfirmOrganizationUserValidationRequest request,
        Guid orgId,
        Dictionary<Guid, bool> isTwoFactorEnabledByUserId,
        Dictionary<Guid, RequireTwoFactorPolicyRequirement> requireTwoFactorByUserId,
        Dictionary<Guid, AutomaticUserConfirmationPolicyRequirement> autoConfirmPolicyByUserId,
        HashSet<Guid> providerUserIds,
        Dictionary<Guid, int> orgUserCountByUserId)
    {
        var userId = request.OrganizationUser!.UserId!.Value;

        // User must have 2FA enabled if the org's RequireTwoFactor policy is active.
        if (!isTwoFactorEnabledByUserId.GetValueOrDefault(userId) &&
            requireTwoFactorByUserId.TryGetValue(userId, out var req) &&
            req.IsTwoFactorRequiredForOrganization(orgId))
        {
            return Invalid(request, new UserDoesNotHaveTwoFactorEnabled());
        }

        if (autoConfirmPolicyByUserId.GetValueOrDefault(userId) is { } autoConfirmPolicyRequirement)
        {
            var orgMembershipCount = orgUserCountByUserId.GetValueOrDefault(userId);
            var violation = autoConfirmPolicyEnforcementValidator.GetAutoConfirmPolicyViolation(
                autoConfirmPolicyRequirement, orgId, providerUserIds.Contains(userId), orgMembershipCount);
            if (violation is not null)
            {
                return Invalid(request, violation);
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
        if (request.OrganizationUser is null)
        {
            return Invalid(request, new UserNotFoundError());
        }

        if (request.OrganizationUser.UserId is null)
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
        PolicyStatus PolicyStatus,
        Dictionary<Guid, bool> IsTwoFactorEnabledByUserId,
        Dictionary<Guid, RequireTwoFactorPolicyRequirement> RequireTwoFactorByUserId,
        Dictionary<Guid, AutomaticUserConfirmationPolicyRequirement> AutoConfirmPolicyByUserId,
        Dictionary<Guid, int> OrgUserCountByUserId,
        HashSet<Guid> ProviderUserIds
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
            twoFactorTask.Result.ToDictionary(r => r.userId, r => r.twoFactorIsEnabled),
            requireTwoFactorTask.Result.ToDictionary(r => r.UserId, r => r.Requirement),
            autoConfirmTask.Result.ToDictionary(r => r.UserId, r => r.Requirement),
            orgUsersTask.Result.GroupBy(ou => ou.UserId!.Value).ToDictionary(g => g.Key, g => g.Count()),
            providerUsersTask.Result.Select(pu => pu.UserId!.Value).ToHashSet()
        );
    }
}
