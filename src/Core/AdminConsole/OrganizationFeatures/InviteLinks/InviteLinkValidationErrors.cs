using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

// The three user invite-link endpoints (validate/accept, invite, confirm) surface failures as RFC 7807
// problems with a stable, client-localizable Type code. Each error below implements IValidationError so the
// API layer routes it through BitwardenValidationProblem, and carries its own message, property name and
// HTTP status. StatusCode is declared virtual on the base record so a derived override is visible through
// IValidationError. The plain records in Errors.cs remain for the admin endpoints.
public abstract record InviteLinkValidationError(string Message) : Error(Message), IValidationError
{
    public abstract string PropertyName { get; }
    public abstract string Type { get; }
    public virtual int StatusCode => 400;
}

public sealed record InviteLinkNotFoundError()
    : InviteLinkValidationError("Invite link not found.")
{
    public override string PropertyName => "code";
    public override string Type => "invite_link_not_found";
    public override int StatusCode => 404;
}

public sealed record InviteLinkNotAvailableError()
    : InviteLinkValidationError("Your organization's plan does not support invite links.")
{
    public override string PropertyName => "code";
    public override string Type => "invite_link_not_available";
}

public sealed record InviteLinkConfirmationNotSupportedError()
    : InviteLinkValidationError("This invite link does not support confirmation.")
{
    public override string PropertyName => "code";
    public override string Type => "invite_link_confirmation_not_supported";
}

public sealed record EmailNotVerifiedError()
    : InviteLinkValidationError("You must verify your email address before joining an organization.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "email_not_verified";
}

public sealed record EmailDomainNotAllowedError(string OrgName)
    : InviteLinkValidationError($"You're not allowed to join the {OrgName} vault with your email domain.")
{
    public override string PropertyName => "code";
    public override string Type => "email_domain_not_allowed";
}

public sealed record ProviderUsersCannotJoinError()
    : InviteLinkValidationError("Provider users cannot join organization vaults via invite link.")
{
    public override string PropertyName => "code";
    public override string Type => "provider_users_cannot_join";
}

public sealed record OrganizationAccessRevokedError(string OrgName)
    : InviteLinkValidationError($"Your access to the {OrgName} vault has been revoked.")
{
    public override string PropertyName => "code";
    public override string Type => "organization_access_revoked";
}

public sealed record AlreadyOrganizationMemberError(string OrgName)
    : InviteLinkValidationError($"You're already a member of {OrgName}.")
{
    public override string PropertyName => "code";
    public override string Type => "already_organization_member";
}

public sealed record OrganizationHasNoAvailableSeatsError(string OrgName)
    : InviteLinkValidationError($"The {OrgName} vault has no available seats.")
{
    public override string PropertyName => "code";
    public override string Type => "organization_has_no_available_seats";
}

public sealed record SeatAddFailedError()
    : InviteLinkValidationError("Unable to join this vault right now. Please contact your organization admin.")
{
    public override string PropertyName => "code";
    public override string Type => "seat_add_failed";
}

public sealed record ResetPasswordKeyRequiredError()
    : InviteLinkValidationError("Master Password reset is required, but not provided.")
{
    public override string PropertyName => "resetPasswordKey";
    public override string Type => "reset_password_key_required";
}

public sealed record MemberOfAnotherOrganizationError()
    : InviteLinkValidationError("Member cannot join this organization vault until they leave all other organization vaults.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "member_of_another_organization";
}

public sealed record SingleOrganizationPolicyError()
    : InviteLinkValidationError("Member cannot join this organization's vault because they are a member of another organization which forbids it.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "single_organization_policy";
}

public sealed record TwoFactorRequiredForMembershipError()
    : InviteLinkValidationError("You cannot join this organization vault until you enable two-step login on your user account.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "two_factor_required_for_membership";
}

public sealed record AutoConfirmPolicyViolationOtherOrgError(string Email)
    : InviteLinkValidationError($"Cannot confirm {Email} because they are a member of another organization which forbids it.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "auto_confirm_policy_violation_other_org";
}

public sealed record AutoConfirmPolicyViolationTargetOrgError(string Email)
    : InviteLinkValidationError($"Cannot confirm {Email} until they leave all other organization vaults.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "auto_confirm_policy_violation_target_org";
}

public sealed record OnlyOneFreeOrganizationAdminAllowedError()
    : InviteLinkValidationError("You can only be an admin of 1 free organization vault.")
{
    public override string PropertyName => "organizationId";
    public override string Type => "only_one_free_organization_admin_allowed";
}
