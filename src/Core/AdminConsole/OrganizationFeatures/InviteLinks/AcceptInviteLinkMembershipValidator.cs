using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IAcceptInviteLinkMembershipValidator"/>.
/// </summary>
public class AcceptInviteLinkMembershipValidator(
    IPolicyQuery policyQuery,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IProviderUserRepository providerUserRepository,
    IOrganizationUserRepository organizationUserRepository)
    : IAcceptInviteLinkMembershipValidator
{
    public async Task<ValidationResult<AcceptInviteLinkMembershipValidationRequest>> ValidateAsync(
        AcceptInviteLinkMembershipValidationRequest request)
    {
        var user = request.User;
        var organization = request.Organization;

        // Check membership status first for a friendlier message if they're already in the organization
        var membershipStatusError = ValidateExistingMembershipStatus(request.ExistingMembership, organization.DisplayName());
        if (membershipStatusError is not null)
        {
            return Invalid(request, membershipStatusError);
        }

        // Security: allowed domains only provide protection if the user has proven control over their email address.
        if (!user.EmailVerified)
        {
            return Invalid(request, new EmailNotVerified());
        }

        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, request.AllowedDomains))
        {
            return Invalid(request, new EmailDomainNotAllowed(organization.DisplayName()));
        }

        // Provider users are not blocked outright: they are exempt from Single Org and 2FA (matching those
        // policies' ExemptProviders = true), but are still blocked from an Auto-Confirm organization
        // (ExemptProviders = false). This mirrors AutomaticUserConfirmationPolicyEnforcementHandler. See
        // ValidatePoliciesAsync for where the block is applied.
        var isProviderUser = (await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0;

        // An email invite can carry an Admin/Owner role, so the "one admin of a Free org" rule (a plan
        // constraint, not a policy) is enforced before the policy checks, mirroring AcceptOrgUserCommand.
        var freeAdminError = await ValidateFreeOrganizationAdminLimitAsync(request);
        if (freeAdminError is not null)
        {
            return Invalid(request, freeAdminError);
        }

        // ----- Policy checks -----
        var policyError = await ValidatePoliciesAsync(request, isProviderUser);
        if (policyError is not null)
        {
            return Invalid(request, policyError);
        }

        // ----- Account recovery auto-enroll -----
        // Whether auto-enroll applies is resolved by the caller and carried on the request.
        if (request.AccountRecoveryAutoEnroll && !OrganizationUser.IsValidResetPasswordKey(request.ResetPasswordKey))
        {
            return Invalid(request, new ResetPasswordKeyRequired());
        }

        return Valid(request);
    }

    /// <summary>
    /// Enforces the target org's Auto-Confirm, Single-Org, and 2FA policies for every accept path (brand-new
    /// member, Staged provisioning, or existing email invitation). The user's other organizations are evaluated
    /// through the requirement framework (cross-org), while the target org's policies are read directly and
    /// paired with <see cref="Organization.UsePolicies"/>.
    /// </summary>
    /// <remarks>
    /// A direct <see cref="IPolicyQuery"/> read is raw: it applies none of the role/status/provider exemptions
    /// that <see cref="IPolicyRequirementQuery"/> applies via <c>BasePolicyRequirementFactory.Enforce</c>. Those
    /// exemptions are therefore replicated inline here for the target org — see
    /// <see cref="IsExemptFromTargetOrgPolicy"/> and the provider block in the Auto-Confirm branch, with
    /// <c>AutomaticUserConfirmationPolicyEnforcementHandler</c> as the source of truth. Only the role and provider
    /// axes matter for the target org: the status axis never does, because Single-Org, 2FA, and Auto-Confirm all
    /// set <c>ExemptStatuses = []</c>. Enforcing policies on a Staged row at acceptance is intentional for the same
    /// reason, even though a staged member is not subject to policies while staged.
    /// This inline duplication is a stopgap that must be revisited in milestone 3 (PM-34429), where the confirm
    /// flow hits the same problem.
    /// </remarks>
    private async Task<Error?> ValidatePoliciesAsync(
        AcceptInviteLinkMembershipValidationRequest request, bool isProviderUser)
    {
        var user = request.User;
        var organization = request.Organization;
        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);
        var isMemberOfAnotherOrganization =
            allOrganizationMemberships.Any(ou => ou.OrganizationId != organization.Id);

        // The role of the membership being accepted, used to replicate the target org's role exemptions.
        // Brand-new and Staged members are always User; only an existing email invitation can carry an elevated role.
        var targetRole = request.ExistingMembership?.Type ?? OrganizationUserType.User;

        // Automatic User Confirmation (never role- or provider-exempt).
        // Cross-org: evaluated through the requirement framework, which also covers the target org when it
        // resolves a real membership row (a resolvable Invited row yields the same error as the direct read below;
        // a Staged row cannot be resolved, which is exactly why the direct target read is required).
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(organization.Id))
        {
            return new OtherOrganizationDoesNotAllowOtherMembership(user.Email);
        }

        // The target org's Auto-Confirm state is resolved by the caller and carried on the request.
        if (request.AutoConfirmPolicyEnabled)
        {
            // Provider users are enforced by Auto-Confirm (ExemptProviders = false); block before the multi-org
            // check, mirroring AutomaticUserConfirmationPolicyEnforcementHandler.
            if (isProviderUser)
            {
                return new ProviderUsersCannotAcceptInviteLink();
            }

            if (isMemberOfAnotherOrganization)
            {
                return new UserCannotBelongToAnotherOrganization(user.Email);
            }
        }

        // Single Organization (Owner/Admin and provider users are exempt).
        var singleOrgRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organization.Id, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        if (!IsExemptFromTargetOrgPolicy(targetRole, isProviderUser, PolicyType.SingleOrg)
            && await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.SingleOrg)
            && isMemberOfAnotherOrganization)
        {
            return new UserIsAMemberOfAnotherOrganization();
        }

        // Two-Factor Authentication (Owner/Admin and provider users are exempt).
        if (!IsExemptFromTargetOrgPolicy(targetRole, isProviderUser, PolicyType.TwoFactorAuthentication)
            && !await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user)
            && await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.TwoFactorAuthentication))
        {
            return new TwoFactorRequiredForMembership();
        }

        return null;
    }

    /// <summary>
    /// Replicates the target org's role and provider exemptions for a direct <see cref="IPolicyQuery"/> read,
    /// matching the requirement factories' defaults: Single Org and 2FA exempt Owners/Admins
    /// (<c>ExemptRoles</c>) and provider users (<c>ExemptProviders = true</c>). Automatic User Confirmation is
    /// deliberately absent — it exempts no role or provider (<c>ExemptRoles = []</c>, <c>ExemptProviders = false</c>).
    /// </summary>
    private static bool IsExemptFromTargetOrgPolicy(OrganizationUserType role, bool isProviderUser, PolicyType policyType)
        => policyType is PolicyType.SingleOrg or PolicyType.TwoFactorAuthentication
            && (role is OrganizationUserType.Owner or OrganizationUserType.Admin || isProviderUser);

    // An email invite can carry an Admin/Owner role. Enforce the "one admin of a Free org" rule, mirroring
    // AcceptOrgUserCommand, so the invite link cannot bypass it. No-op for brand-new/Staged members (always User).
    private async Task<Error?> ValidateFreeOrganizationAdminLimitAsync(AcceptInviteLinkMembershipValidationRequest request)
    {
        if (request.ExistingMembership?.Type is OrganizationUserType.Owner or OrganizationUserType.Admin &&
            request.Organization.PlanType == PlanType.Free &&
            await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(request.User.Id) > 0)
        {
            return new OnlyOneFreeOrganizationAdminAllowed();
        }

        return null;
    }

    /// <summary>
    /// Reads a policy directly for the organization being joined, paired with
    /// <see cref="Organization.UsePolicies"/> so a policy is never enforced when the organization's plan
    /// does not support policies.
    /// </summary>
    private async Task<bool> IsPolicyEnabledForOrganizationAsync(Organization organization, PolicyType policyType)
        => organization.UsePolicies && (await policyQuery.RunAsync(organization.Id, policyType)).Enabled;

    private static Error? ValidateExistingMembershipStatus(OrganizationUser? existingOrganizationUser, string orgName) =>
        existingOrganizationUser?.Status switch
        {
            OrganizationUserStatusType.Revoked => new OrganizationAccessRevoked(orgName),
            OrganizationUserStatusType.Accepted or OrganizationUserStatusType.Confirmed => new AlreadyOrganizationMember(orgName),
            _ => null
        };
}
