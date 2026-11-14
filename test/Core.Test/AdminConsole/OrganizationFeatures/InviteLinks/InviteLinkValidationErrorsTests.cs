using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

public class InviteLinkValidationErrorsTests
{
    private const string OrgName = "Acme";
    private const string Email = "user@example.com";

    // The type codes and property names are a public contract: the SDK maps each code to a typed error that
    // clients localize. Changing or removing one breaks released clients, so add new codes rather than editing
    // these rows.
    public static IEnumerable<object[]> Errors() =>
    [
        [new InviteLinkNotFoundError(), "invite_link_not_found", "code", "Invite link not found.", true],
        [new InviteLinkNotAvailableError(), "invite_link_not_available", "code", "Your organization's plan does not support invite links.", false],
        [new InviteLinkConfirmationNotSupportedError(), "invite_link_confirmation_not_supported", "code", "This invite link does not support confirmation.", false],
        [new EmailNotVerifiedError(), "email_not_verified", "organizationId", "You must verify your email address before joining an organization.", false],
        [new EmailDomainNotAllowedError(OrgName), "email_domain_not_allowed", "code", $"You're not allowed to join the {OrgName} vault with your email domain.", false],
        [new ProviderUsersCannotJoinError(), "provider_users_cannot_join", "code", "Provider users cannot join organization vaults via invite link.", false],
        [new OrganizationAccessRevokedError(OrgName), "organization_access_revoked", "code", $"Your access to the {OrgName} vault has been revoked.", false],
        [new AlreadyOrganizationMemberError(OrgName), "already_organization_member", "code", $"You're already a member of {OrgName}.", false],
        [new OrganizationHasNoAvailableSeatsError(OrgName), "organization_has_no_available_seats", "code", $"The {OrgName} vault has no available seats.", false],
        [new SeatAddFailedError(), "seat_add_failed", "code", "Unable to join this vault right now. Please contact your organization admin.", false],
        [new ResetPasswordKeyRequiredError(), "reset_password_key_required", "resetPasswordKey", "Master Password reset is required, but not provided.", false],
        [new MemberOfAnotherOrganizationError(), "member_of_another_organization", "organizationId", "Member cannot join this organization vault until they leave all other organization vaults.", false],
        [new SingleOrganizationPolicyError(), "single_organization_policy", "organizationId", "Member cannot join this organization's vault because they are a member of another organization which forbids it.", false],
        [new TwoFactorRequiredForMembershipError(), "two_factor_required_for_membership", "organizationId", "You cannot join this organization vault until you enable two-step login on your user account.", false],
        [new AutoConfirmPolicyViolationOtherOrgError(Email), "auto_confirm_policy_violation_other_org", "organizationId", $"Cannot confirm {Email} because they are a member of another organization which forbids it.", false],
        [new AutoConfirmPolicyViolationTargetOrgError(Email), "auto_confirm_policy_violation_target_org", "organizationId", $"Cannot confirm {Email} until they leave all other organization vaults.", false],
        [new OnlyOneFreeOrganizationAdminAllowedError(), "only_one_free_organization_admin_allowed", "organizationId", "You can only be an admin of 1 free organization vault.", false],
    ];

    [Theory]
    [MemberData(nameof(Errors))]
    public void InviteLinkValidationError_ExposesStableWireContract(
        IValidationError error, string type, string propertyName, string message, bool isNotFound)
    {
        Assert.Equal(type, error.Type);
        Assert.Equal(propertyName, error.PropertyName);
        Assert.Equal(message, error.Message);
        Assert.Equal(isNotFound, error is INotFoundError);
    }
}
