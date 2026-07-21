namespace Bit.Sso.Exceptions;

/// <summary>
/// Thrown when SSO authentication is refused because just-in-time (JIT) provisioning is disabled for
/// the organization and the incoming login has no existing Bitwarden account and no existing
/// organization membership to attach to. Only truly net-new logins reach this exception; users who
/// were invited by an admin or provisioned via SCIM/Directory Connector arrive with a pre-existing
/// <see cref="Bit.Core.Entities.OrganizationUser"/> row and are unaffected.
///
/// The security-critical property matches the sibling SSO refusals: no <c>User</c>, no
/// <c>SsoUser</c>, and no auth session are created. The callback catches this and redirects the user
/// back to the web client's /login with an error code prompting them to contact their administrator
/// to be provisioned first.
/// </summary>
public class SsoAuthnRequiresPreexistingMembershipException : Exception
{
    public Guid OrganizationId { get; }
    public string OrganizationDisplayName { get; }
    public string UserEmail { get; }

    public SsoAuthnRequiresPreexistingMembershipException(
        Guid organizationId, string organizationDisplayName, string userEmail)
        : base($"JIT provisioning is disabled; pre-existing membership required before SSO for org '{organizationDisplayName}'.")
    {
        OrganizationId = organizationId;
        OrganizationDisplayName = organizationDisplayName;
        UserEmail = userEmail;
    }
}
