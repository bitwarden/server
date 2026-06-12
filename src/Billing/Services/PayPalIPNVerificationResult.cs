namespace Bit.Billing.Services;

/// <summary>
/// The outcome of verifying a PayPal IPN message via the <c>cmd=_notify-validate</c> postback handshake.
/// </summary>
public enum PayPalIPNVerificationResult
{
    /// <summary>
    /// PayPal confirmed the message is authentic (postback returned <c>VERIFIED</c>).
    /// </summary>
    Verified,

    /// <summary>
    /// PayPal reported the message is not genuine (postback returned <c>INVALID</c>). The message must not be processed.
    /// </summary>
    Invalid,

    /// <summary>
    /// The postback could not be completed (network error, timeout, non-success status code, or an unrecognized
    /// response body). This is a transient/indeterminate outcome: callers should fail open rather than drop a
    /// legitimate payment, because the postback is a defense-in-depth layer behind the network ACL and webhook key.
    /// </summary>
    Unverified
}
