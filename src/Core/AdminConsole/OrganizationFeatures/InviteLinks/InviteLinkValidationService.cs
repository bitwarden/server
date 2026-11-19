using System.Diagnostics;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
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
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationUserValidationService organizationUserValidationService,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService,
    IGlobalSettings globalSettings)
    : IInviteLinkValidationService
{
    /// <inheritdoc />
    public InviteLinkValidationError? ValidateEligibility(
        OrganizationInviteLink? inviteLink,
        Guid code,
        Organization? organization,
        User user)
    {
        if (inviteLink is null || !inviteLink.CodeMatches(code.ToString()))
        {
            return new InviteLinkNotFoundError();
        }

        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFoundError();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailableError();
        }

        // Order matters: the client starts email verification on EmailNotVerifiedError, so reject disallowed domains
        // first. Otherwise the user would verify their email only to be rejected afterwards.
        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, inviteLink.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowedError(organization.DisplayName());
        }

        // Security: allowed domains only provide protection if the user has proven control over their email address.
        // Self-hosted instances may opt out, but cloud always requires verification.
        var requireEmailVerification = !globalSettings.SelfHosted || globalSettings.EnableOpenInviteLinkEmailVerification;
        if (!user.EmailVerified && requireEmailVerification)
        {
            return new EmailNotVerifiedError();
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
            return new JoinRequirementsValidationResult(ToInviteLinkValidationError(freeAdminError), AutoConfirmPolicyEnabled: false);
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
            return new JoinRequirementsValidationResult(ToInviteLinkValidationError(autoConfirmResult.Error!), autoConfirmResult.AutoConfirmPolicyEnabled);
        }

        var singleOrgError = await joinOrganizationPolicyValidationService.ValidateSingleOrganizationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (singleOrgError is not null)
        {
            return new JoinRequirementsValidationResult(ToInviteLinkValidationError(singleOrgError), autoConfirmResult.AutoConfirmPolicyEnabled);
        }

        var twoFactorError = await joinOrganizationPolicyValidationService.ValidateTwoFactorAuthenticationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer);

        return new JoinRequirementsValidationResult(
            twoFactorError is null ? null : ToInviteLinkValidationError(twoFactorError),
            autoConfirmResult.AutoConfirmPolicyEnabled);
    }

    public async Task<InviteLinkValidationError?> ValidateSeatAvailabilityAsync(
        Organization organization,
        OrganizationUser? existingOrganizationUser)
    {
        // Only a new or Staged membership takes up a new seat.
        if (existingOrganizationUser is not null && existingOrganizationUser.Status != OrganizationUserStatusType.Staged)
        {
            return null;
        }

        var occupiedSeatCount = (await organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;

        return OrganizationSeatAvailability.HasAvailableSeats(organization, occupiedSeatCount)
            ? null
            : new OrganizationHasNoAvailableSeatsError(organization.DisplayName());
    }

    /// <summary>
    /// Translates an error from shared code into its coded invite link error. Throws for an unmapped error so a
    /// new shared error can't silently reach the endpoints as a plain 400.
    /// </summary>
    private static InviteLinkValidationError ToInviteLinkValidationError(Error error) =>
        error switch
        {
            CannotBeAdminOfMultipleFreeOrganizations => new OnlyOneFreeOrganizationAdminAllowedError(),
            ProviderUsersCannotJoin => new ProviderUsersCannotJoinError(),
            OtherOrganizationDoesNotAllowOtherMembership e => new AutoConfirmPolicyViolationOtherOrgError(e.Email),
            UserCannotBelongToAnotherOrganization e => new AutoConfirmPolicyViolationTargetOrgError(e.Email),
            UserIsAMemberOfAnotherOrganization => new MemberOfAnotherOrganizationError(),
            UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => new SingleOrganizationPolicyError(),
            TwoFactorRequiredForMembership => new TwoFactorRequiredForMembershipError(),
            _ => throw new UnreachableException($"Unmapped join requirement error: {error.GetType().Name}")
        };
}
