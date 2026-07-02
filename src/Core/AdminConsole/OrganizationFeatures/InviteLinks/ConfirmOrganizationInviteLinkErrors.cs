using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

// The link-confirm endpoint surfaces its 400 failures as RFC 7807 validation problems with a stable,
// client-localizable Type code. Each error below inherits the shared invite-link/policy error — reusing
// its message and preserving its type identity — and adds IValidationError so the API layer routes it
// through BitwardenValidationProblem. The accept endpoint keeps returning the plain base errors, so its
// responses are unchanged.

public record ConfirmInviteLinkNotAvailable()
    : InviteLinkNotAvailable(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "invite_link_not_available";
}

public record ConfirmEmailDomainNotAllowed()
    : EmailDomainNotAllowed(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "email_domain_not_allowed";
}

public record ConfirmProviderUsersCannotAcceptInviteLink()
    : ProviderUsersCannotAcceptInviteLink(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "provider_users_cannot_join";
}

public record ConfirmOrganizationAccessRevoked()
    : OrganizationAccessRevoked(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "organization_access_revoked";
}

public record ConfirmAlreadyOrganizationMember()
    : AlreadyOrganizationMember(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "already_organization_member";
}

public record ConfirmOrganizationHasNoAvailableSeats()
    : OrganizationHasNoAvailableSeats(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "organization_has_no_available_seats";
}

public record ConfirmSeatAddFailed()
    : SeatAddFailed(), IValidationError
{
    public string PropertyName => "code";
    public string Type => "seat_add_failed";
}

public record ConfirmResetPasswordKeyRequired()
    : ResetPasswordKeyRequired(), IValidationError
{
    public string PropertyName => "resetPasswordKey";
    public string Type => "reset_password_key_required";
}

public record ConfirmUserIsAMemberOfAnotherOrganization()
    : UserIsAMemberOfAnotherOrganization(), IValidationError
{
    public string PropertyName => "organizationId";
    public string Type => "member_of_another_organization";
}

public record ConfirmUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy()
    : UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy(), IValidationError
{
    public string PropertyName => "organizationId";
    public string Type => "single_organization_policy";
}

public record ConfirmTwoFactorRequiredForMembership()
    : TwoFactorRequiredForMembership(), IValidationError
{
    public string PropertyName => "organizationId";
    public string Type => "two_factor_required_for_membership";
}
