namespace Bit.Sso.Utilities.Saml2;

/// <summary>
/// Runs the RSA 1.5 deprecation notice in the background, so the SAML login request does not wait for it.
/// </summary>
public interface ISaml2Rsa15DeprecationNotifier
{
    /// <summary>
    /// Starts a background email send of the RSA 1.5 deprecation notice for an organization.
    /// Returns at once and never throws.
    /// </summary>
    /// <remarks>
    /// When an email send for the organization is already pending on this server instance, this method drops the call.
    /// </remarks>
    /// <param name="organizationId">The organization to notify.</param>
    void TryQueue(Guid organizationId);
}
