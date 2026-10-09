namespace Bit.Core.Auth.Sso;

/// <summary>
/// Limits how often an organization receives the RSA 1.5 deprecation email.
/// The interval claims the per-organization interval in the persistent cache.
/// </summary>
public interface ISaml2Rsa15DeprecationNoticeInterval
{
    /// <summary>
    /// Checks the RSA 1.5 deprecation email send interval for an organization.
    /// </summary>
    /// <param name="organizationId">The organization to check.</param>
    /// <returns>
    /// <c>true</c> and records current time when a wait interval is not active
    /// for the given organization.
    /// Otherwise <c>false</c>.
    /// </returns>
    Task<bool> TryClaimIntervalAsync(Guid organizationId);
}
