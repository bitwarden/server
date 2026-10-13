using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

/// <inheritdoc />
public class JoinOrganizationPolicyValidationService(
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IOrganizationUserRepository organizationUserRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IProviderUserRepository providerUserRepository)
    : IJoinOrganizationPolicyValidationService
{
    public async Task<Error?> ValidateSingleOrganizationPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole) =>
        await ValidateSingleOrganizationPolicyAsync(organizationId, user, proposedRole,
            await preAccessEnforcerQuery.RunAsync(organizationId),
            await organizationUserRepository.GetManyByUserAsync(user.Id));

    public async Task<Error?> ValidateSingleOrganizationPolicyAsync(Guid organizationId, User user,
        OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer,
        ICollection<OrganizationUser> allOrganizationMemberships)
    {
        // Other organizations (may also cover this one; consistent with the check below)
        var singleOrgRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organizationId, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        // This organization
        if (IsMemberOfAnotherOrganization(allOrganizationMemberships, organizationId)
            && policyEnforcer.Evaluate(PolicyType.SingleOrg, user.Id, proposedRole).IsEnforced)
        {
            return new UserIsAMemberOfAnotherOrganization();
        }

        return null;
    }

    public async Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole) =>
        await ValidateTwoFactorAuthenticationPolicyAsync(organizationId, user, proposedRole,
            await preAccessEnforcerQuery.RunAsync(organizationId));

    public async Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(Guid organizationId, User user,
        OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer)
    {
        if (policyEnforcer.Evaluate(PolicyType.TwoFactorAuthentication, user.Id, proposedRole).IsEnforced
            && !await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return new TwoFactorRequiredForMembership();
        }

        return null;
    }

    public async Task<AutomaticUserConfirmationPolicyValidationResult> ValidateAutomaticUserConfirmationPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole) =>
        await ValidateAutomaticUserConfirmationPolicyAsync(organizationId, user, proposedRole,
            await preAccessEnforcerQuery.RunAsync(organizationId),
            await organizationUserRepository.GetManyByUserAsync(user.Id));

    public async Task<AutomaticUserConfirmationPolicyValidationResult> ValidateAutomaticUserConfirmationPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer,
        ICollection<OrganizationUser> allOrganizationMemberships)
    {
        var autoConfirmPolicyEnabled = policyEnforcer
            .Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, proposedRole)
            .IsEnforced;

        // Other organizations
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(organizationId))
        {
            return new AutomaticUserConfirmationPolicyValidationResult(
                new OtherOrganizationDoesNotAllowOtherMembership(user.Email), autoConfirmPolicyEnabled);
        }

        // This organization
        if (!autoConfirmPolicyEnabled)
        {
            return new AutomaticUserConfirmationPolicyValidationResult(null, autoConfirmPolicyEnabled);
        }

        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return new AutomaticUserConfirmationPolicyValidationResult(new ProviderUsersCannotJoin(), autoConfirmPolicyEnabled);
        }

        if (IsMemberOfAnotherOrganization(allOrganizationMemberships, organizationId))
        {
            return new AutomaticUserConfirmationPolicyValidationResult(
                new UserCannotBelongToAnotherOrganization(user.Email), autoConfirmPolicyEnabled);
        }

        return new AutomaticUserConfirmationPolicyValidationResult(null, autoConfirmPolicyEnabled);
    }

    public async Task<AccountRecoveryPolicyValidationResult> ValidateAccountRecoveryPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole, string? resetPasswordKey) =>
        await ValidateAccountRecoveryPolicyAsync(organizationId, user, proposedRole,
            await preAccessEnforcerQuery.RunAsync(organizationId), resetPasswordKey);

    public Task<AccountRecoveryPolicyValidationResult> ValidateAccountRecoveryPolicyAsync(Guid organizationId,
        User user, OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer, string? resetPasswordKey)
    {
        var decision = policyEnforcer.Evaluate(PolicyType.ResetPassword, user.Id, proposedRole);
        var autoEnrollEnabled = decision.IsEnforced
            && decision.GetDataModel<ResetPasswordDataModel>().AutoEnrollEnabled;

        var error = autoEnrollEnabled && !OrganizationUser.IsValidResetPasswordKey(resetPasswordKey)
            ? new ResetPasswordKeyRequired()
            : null;

        return Task.FromResult(new AccountRecoveryPolicyValidationResult(error, autoEnrollEnabled));
    }

    private static bool IsMemberOfAnotherOrganization(
        ICollection<OrganizationUser> allOrganizationMemberships, Guid organizationId) =>
        allOrganizationMemberships.Any(ou => ou.OrganizationId != organizationId);
}
