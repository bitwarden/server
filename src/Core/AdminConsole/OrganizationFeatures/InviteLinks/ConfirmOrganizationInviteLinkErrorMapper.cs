using System.Diagnostics;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Translates the plain errors returned by the shared invite link validators into the link-confirm
/// <see cref="IValidationError"/> variants, so the confirm endpoint always returns the RFC 7807 shape.
/// </summary>
public static class ConfirmOrganizationInviteLinkErrorMapper
{
    /// <summary>
    /// Maps <paramref name="error"/> to its link-confirm validation-problem variant.
    /// </summary>
    /// <exception cref="UnreachableException">
    /// The error has no mapping. A new validator error must be mapped here rather than silently falling back to a
    /// plain 400.
    /// </exception>
    public static Error ToValidationError(Error error) => error switch
    {
        IValidationError => error,
        InviteLinkNotFound => error,
        InviteLinkNotAvailable => new ConfirmInviteLinkNotAvailable(),
        EmailDomainNotAllowed e => new ConfirmEmailDomainNotAllowed(e.OrgName),
        EmailNotVerified => new ConfirmEmailNotVerified(),
        OrganizationAccessRevoked e => new ConfirmOrganizationAccessRevoked(e.OrgName),
        AlreadyOrganizationMember e => new ConfirmAlreadyOrganizationMember(e.OrgName),
        OnlyOneFreeOrganizationAdminAllowed => new ConfirmOnlyOneFreeOrganizationAdminAllowed(),
        OrganizationHasNoAvailableSeats e => new ConfirmOrganizationHasNoAvailableSeats(e.OrgName),
        UserIsAMemberOfAnotherOrganization => new ConfirmUserIsAMemberOfAnotherOrganization(),
        UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => new ConfirmUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy(),
        TwoFactorRequiredForMembership => new ConfirmTwoFactorRequiredForMembership(),
        OtherOrganizationDoesNotAllowOtherMembership e => new ConfirmOtherOrganizationDoesNotAllowOtherMembership(e.Email),
        UserCannotBelongToAnotherOrganization e => new ConfirmUserCannotBelongToAnotherOrganization(e.Email),
        ProviderUsersCannotAcceptInviteLink => new ConfirmProviderUsersCannotAcceptInviteLink(),
        _ => throw new UnreachableException($"Unmapped invite link confirmation error: {error.GetType().Name}")
    };
}
