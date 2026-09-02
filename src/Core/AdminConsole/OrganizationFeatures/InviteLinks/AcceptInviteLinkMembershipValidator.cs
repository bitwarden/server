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
    IProviderOrganizationRepository providerOrganizationRepository,
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

        // Security: check the user is part of an allowed domain.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, request.AllowedDomains))
        {
            return Invalid(request, new EmailDomainNotAllowed(organization.DisplayName()));
        }

        // An email invite can carry an Admin/Owner role, so the "one admin of a Free org" rule (a plan
        // constraint, not a policy) is enforced before the policy checks, mirroring AcceptOrgUserCommand.
        var freeAdminError = await ValidateFreeOrganizationAdminLimitAsync(request);
        if (freeAdminError is not null)
        {
            return Invalid(request, freeAdminError);
        }

        // Policy checks
        var policyError = await ValidatePoliciesAsync(request);
        if (policyError is not null)
        {
            return Invalid(request, policyError);
        }

        // Account recovery auto-enroll requirement
        if (request.AccountRecoveryAutoEnroll && !OrganizationUser.IsValidResetPasswordKey(request.ResetPasswordKey))
        {
            return Invalid(request, new ResetPasswordKeyRequired());
        }

        return Valid(request);
    }

    /// <summary>
    /// Enforces the target org's Auto-Confirm, Single-Org, and 2FA policies for every accept path: brand-new
    /// member, Staged provisioning, or Invited by email (and for some reason clicked the open link anyway).
    /// </summary>
    /// <remarks>
    /// This cannot share the standard accept command checks because for brand-new members there is no
    /// OrganizationUser row in the database yet, and <see cref="IPolicyRequirementQuery"/> will not return the
    /// target organization's policies. That query also filters out Staged users for the target organization.
    /// Instead, <see cref="IPolicyRequirementQuery"/> is only used to enforce other organization's policies,
    /// and we use <see cref="IPolicyQuery"/> directly (plus duplicate policy enforcement logic) for the target
    /// organization.
    /// This duplication is a stopgap that must be revisited in milestone 3 (PM-34429), where the confirm
    /// flow hits the same problem.
    /// </remarks>
    private async Task<Error?> ValidatePoliciesAsync(AcceptInviteLinkMembershipValidationRequest request)
    {
        var user = request.User;
        var organization = request.Organization;
        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);
        var isMemberOfAnotherOrganization =
            allOrganizationMemberships.Any(ou => ou.OrganizationId != organization.Id);

        var targetRole = request.ExistingMembership?.Type ?? OrganizationUserType.User;

        // Automatic User Confirmation - other organizations (never role- or provider-exempt).
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(organization.Id))
        {
            return new OtherOrganizationDoesNotAllowOtherMembership(user.Email);
        }

        // Automatic User Confirmation - this organization
        if (request.AutoConfirmPolicyEnabled)
        {
            // Autoconfirm enforcement: any provider user cannot be a member of an autoconfirm org.
            var isProviderUserForAnyOrganization = (await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0;
            if (isProviderUserForAnyOrganization)
            {
                return new ProviderUsersCannotAcceptInviteLink();
            }

            // Autoconfirm enforcement: you cannot be a member of another org (no role exemptions)
            if (isMemberOfAnotherOrganization)
            {
                return new UserCannotBelongToAnotherOrganization(user.Email);
            }
        }

        // Single organization policy - other organizations (Owner/Admin and provider users are exempt).
        var singleOrgRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organization.Id, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        var isProviderForOrganization = (await providerOrganizationRepository.GetManyByUserAsync(user.Id))
            .Any(po => po.OrganizationId == organization.Id);

        // Single organization policy - this organization (Owner/Admin and provider users are exempt)
        if (!isProviderForOrganization
            && targetRole is not OrganizationUserType.Owner
            && targetRole is not OrganizationUserType.Admin
            && isMemberOfAnotherOrganization
            && await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.SingleOrg))
        {
            return new UserIsAMemberOfAnotherOrganization();
        }

        // Two-Factor Authentication - this organization (Owner/Admin and provider users are exempt).
        if (!isProviderForOrganization
            && targetRole is not OrganizationUserType.Owner
            && targetRole is not OrganizationUserType.Admin
            && await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.TwoFactorAuthentication)
            && !await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return new TwoFactorRequiredForMembership();
        }

        return null;
    }

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
