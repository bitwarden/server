namespace Bit.Core.Auth.Sso;

/// <summary>
/// Notifies the confirmed owners and admins of an organization by email that their 
/// SAML identity provider uses the deprecated RSA 1.5 encryption method.
/// </summary>
public interface ISaml2Rsa15DeprecationNoticeCommand
{
    /// <summary>
    /// Sends the RSA 1.5 deprecation email to the owners and admins of an organization
    /// when no email send cooldown is active for the organization.
    /// Failures are logged and never thrown.
    /// </summary>
    /// <remarks>
    /// A failed email send removes the email send cooldown, so the next call retries the email send.
    /// An organization with no owner or admin email address keeps the email send cooldown.
    /// A configured email send cooldown of 0 days or less turns the email off.
    /// </remarks>
    /// <param name="organizationId">The organization whose owners and admins are notified.</param>
    Task SendEmailIfDueAsync(Guid organizationId);
}
