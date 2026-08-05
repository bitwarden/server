using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
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
    IAcceptOrganizationMembershipValidator sharedMembershipValidator,
    IPolicyQuery policyQuery,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IProviderUserRepository providerUserRepository,
    IOrganizationUserRepository organizationUserRepository)
    : IAcceptInviteLinkMembershipValidator
{
    public async Task<ValidationResult<AcceptInviteLinkMembershipValidationResult>> ValidateAsync(
        AcceptInviteLinkMembershipValidationRequest request)
    {
        var user = request.User;
        var organization = request.Organization;
        var result = new AcceptInviteLinkMembershipValidationResult();

        // ----- Common eligibility -----
        if (!user.EmailVerified)
        {
            return Invalid(result, new EmailNotVerified());
        }

        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, request.AllowedDomains))
        {
            return Invalid(result, new EmailDomainNotAllowed(organization.DisplayName()));
        }

        // Provider users cannot accept invite links, regardless of the membership branch below.
        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return Invalid(result, new ProviderUsersCannotAcceptInviteLink());
        }

        var membershipStatusError = ValidateExistingMembershipStatus(request.ExistingMembership, organization.DisplayName());
        if (membershipStatusError is not null)
        {
            return Invalid(result, membershipStatusError);
        }

        // ----- Policy checks: split once on membership -----
        // A brand-new member has no OrganizationUser in the target org, so IPolicyRequirementQuery cannot
        // resolve the target org's policies; the target org's policies are read directly instead. An
        // existing pending email invitation has a row, so the requirement framework resolves it correctly
        // and enforcement is delegated to the proven shared validator.
        var policyValidation = request.ExistingMembership is null
            ? await ValidateNewMemberPoliciesAsync(request)
            : await ValidateExistingMemberPoliciesAsync(request);
        if (policyValidation.IsError)
        {
            return Invalid(result, policyValidation.AsError);
        }

        // ----- Account recovery auto-enroll -----
        var autoEnrollEnabled = await IsAccountRecoveryAutoEnrollEnabledAsync(organization);
        if (autoEnrollEnabled && !OrganizationUser.IsValidResetPasswordKey(request.ResetPasswordKey))
        {
            return Invalid(result, new ResetPasswordKeyRequired());
        }

        return Valid(new AcceptInviteLinkMembershipValidationResult
        {
            AutoConfirmPolicyEnabled = policyValidation.Request.AutoConfirmPolicyEnabled,
            AutoEnrollEnabled = autoEnrollEnabled,
        });
    }

    /// <summary>
    /// Enforces the target org's Auto-Confirm, Single-Org, and 2FA policies for a brand-new member. The
    /// user's other organizations are still evaluated through the requirement framework (cross-org), while
    /// the target org's policies are read directly and paired with <see cref="Organization.UsePolicies"/>.
    /// </summary>
    private async Task<ValidationResult<AcceptOrganizationMembershipValidationResult>> ValidateNewMemberPoliciesAsync(
        AcceptInviteLinkMembershipValidationRequest request)
    {
        var user = request.User;
        var organization = request.Organization;
        var result = new AcceptOrganizationMembershipValidationResult();
        var isMemberOfAnotherOrganization =
            request.AllOrganizationMemberships.Any(ou => ou.OrganizationId != organization.Id);

        // Automatic User Confirmation
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(organization.Id))
        {
            return Invalid(result, new OtherOrganizationDoesNotAllowOtherMembership(user.Email));
        }

        var autoConfirmPolicyEnabled =
            await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.AutomaticUserConfirmation);
        if (autoConfirmPolicyEnabled && isMemberOfAnotherOrganization)
        {
            return Invalid(result, new UserCannotBelongToAnotherOrganization(user.Email));
        }

        // Single Organization
        var singleOrgRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organization.Id, request.AllOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return Invalid(result, singleOrgError);
        }

        if (await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.SingleOrg) && isMemberOfAnotherOrganization)
        {
            return Invalid(result, new UserIsAMemberOfAnotherOrganization());
        }

        // Two-Factor Authentication
        if (!await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user)
            && await IsPolicyEnabledForOrganizationAsync(organization, PolicyType.TwoFactorAuthentication))
        {
            return Invalid(result, new TwoFactorRequiredForMembership());
        }

        return Valid(new AcceptOrganizationMembershipValidationResult
        {
            AutoConfirmPolicyEnabled = autoConfirmPolicyEnabled,
        });
    }

    /// <summary>
    /// Enforces policies for an existing pending email invitation by delegating to the shared validator,
    /// whose requirement-framework lookups resolve the target org's policies (the Invited row exists).
    /// </summary>
    private async Task<ValidationResult<AcceptOrganizationMembershipValidationResult>> ValidateExistingMemberPoliciesAsync(
        AcceptInviteLinkMembershipValidationRequest request)
    {
        var freeAdminError = await ValidateFreeOrganizationAdminLimitAsync(request);
        if (freeAdminError is not null)
        {
            return Invalid(new AcceptOrganizationMembershipValidationResult(), freeAdminError);
        }

        return await sharedMembershipValidator.ValidateAsync(new AcceptOrganizationMembershipValidationRequest
        {
            OrganizationId = request.Organization.Id,
            User = request.User,
            AllOrganizationMemberships = request.AllOrganizationMemberships,
            ExistingMembership = request.ExistingMembership,
        });
    }

    // An email invite can carry an Admin/Owner role. Enforce the "one admin of a Free org" rule, mirroring
    // AcceptOrgUserCommand, so the invite link cannot bypass it.
    private async Task<Error?> ValidateFreeOrganizationAdminLimitAsync(AcceptInviteLinkMembershipValidationRequest request)
    {
        var existingMembership = request.ExistingMembership!;
        if (existingMembership.Type is OrganizationUserType.Owner or OrganizationUserType.Admin &&
            request.Organization.PlanType == PlanType.Free &&
            await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(request.User.Id) > 0)
        {
            return new OnlyOneFreeOrganizationAdminAllowed();
        }

        return null;
    }

    private async Task<bool> IsAccountRecoveryAutoEnrollEnabledAsync(Organization organization)
    {
        if (!organization.UsePolicies)
        {
            return false;
        }

        var resetPasswordPolicy = await policyQuery.RunAsync(organization.Id, PolicyType.ResetPassword);
        return resetPasswordPolicy.Enabled
            && resetPasswordPolicy.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled;
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
