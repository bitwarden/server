namespace Bit.Core.Auth.Sso;

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
