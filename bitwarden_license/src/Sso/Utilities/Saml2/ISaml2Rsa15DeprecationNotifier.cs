namespace Bit.Sso.Utilities.Saml2;

public interface ISaml2Rsa15DeprecationNotifier
{
    /// <summary>
    /// Queues the RSA 1.5 deprecation email for an organization.
    /// Returns at once. The claim and the send run in the background.
    /// Never throws.
    /// </summary>
    /// <remarks>
    /// A claim is one call to
    /// <see cref="Bit.Core.Auth.Sso.ISaml2Rsa15DeprecationNoticeInterval.TryClaimIntervalAsync"/>.
    /// The claim records the current time before the send, so a failed send still uses the interval.
    /// A claim is in flight from the moment this method adds the organization to its internal guard 
    /// until the claim returns or throws.
    /// While an organization has a claim processing, this method ignores more calls for that organization
    /// to prevent eager notifications.
    /// </remarks>
    /// <param name="organizationId">The organization to notify.</param>
    void TryQueue(Guid organizationId);
}
