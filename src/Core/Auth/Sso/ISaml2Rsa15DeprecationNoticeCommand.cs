namespace Bit.Core.Auth.Sso;

/// <summary>
/// Notifies the confirmed owners and admins of an organization by email that their 
/// SAML identity provider uses the deprecated RSA 1.5 encryption method.
/// </summary>
public interface ISaml2Rsa15DeprecationNoticeCommand
{
    /// <summary>
    /// Sends the RSA 1.5 deprecation email to the owners and admins of an organization.
    /// Failures are logged and never thrown.
    /// </summary>
    /// <param name="organizationId">The organization whose owners and admins are notified.</param>
    Task SendAsync(Guid organizationId);
}
