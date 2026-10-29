using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <inheritdoc />
public class InviteLinkValidationService(
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationUserValidationService organizationUserValidationService,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService,
    IGlobalSettings globalSettings)
    : IInviteLinkValidationService
{
    /// <inheritdoc />
    public Error? ValidateEligibility(
        OrganizationInviteLink? inviteLink,
        Guid code,
        Organization? organization,
        User user)
    {
        if (inviteLink is null || !inviteLink.CodeMatches(code.ToString()))
        {
            return new InviteLinkNotFound();
        }

        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        // Order matters: the client starts email verification on EmailNotVerified, so reject disallowed domains
        // first. Otherwise the user would verify their email only to be rejected afterwards.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, inviteLink.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowed(organization.DisplayName());
        }

        // Security: allowed domains only provide protection if the user has proven control over their email address.
        // Self-hosted instances may opt out, but cloud always requires verification.
        var requireEmailVerification = !globalSettings.SelfHosted || globalSettings.EnableOpenInviteLinkEmailVerification;
        if (!user.EmailVerified && requireEmailVerification)
        {
            return new EmailNotVerified();
        }

        return null;
    }

    public async Task<JoinRequirementsValidationResult> ValidateJoinRequirementsAsync(
        Organization organization,
        User user,
        OrganizationUserType proposedRole,
        IPreAccessPolicyEnforcer policyEnforcer)
    {
        // The free organization admin count only includes confirmed memberships, so the joining membership
        // has not been counted yet.
        var freeAdminError = await organizationUserValidationService.ValidateFreeOrgAdminLimitAsync(
            user.Id,
            organization.PlanType,
            currentUserType: OrganizationUserType.User,
            newUserType: proposedRole);

        if (freeAdminError is not null)
        {
            return new JoinRequirementsValidationResult(freeAdminError, AutoConfirmPolicyEnabled: false);
        }

        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);

        var autoConfirmResult = await joinOrganizationPolicyValidationService.ValidateAutomaticUserConfirmationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (autoConfirmResult.IsError)
        {
            return new JoinRequirementsValidationResult(autoConfirmResult.Error, autoConfirmResult.AutoConfirmPolicyEnabled);
        }

        var singleOrgError = await joinOrganizationPolicyValidationService.ValidateSingleOrganizationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (singleOrgError is not null)
        {
            return new JoinRequirementsValidationResult(singleOrgError, autoConfirmResult.AutoConfirmPolicyEnabled);
        }

        var twoFactorError = await joinOrganizationPolicyValidationService.ValidateTwoFactorAuthenticationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer);

        return new JoinRequirementsValidationResult(twoFactorError, autoConfirmResult.AutoConfirmPolicyEnabled);
    }
}
