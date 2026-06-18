namespace Bit.Api.Vault.Authorization;

/// <summary>
/// Documents an intentional choice that a controller action returning cipher data does not need PAM
/// credential-leasing filtering — for example, because it only ever handles personal ciphers, or
/// because authorization is enforced out-of-band. The lease-filter fitness test allows actions that
/// carry this marker (and otherwise requires a sanctioned response type), mirroring how
/// <c>NoopAuthorizeAttribute</c> documents intentional non-authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class LeaseFilterExemptAttribute : Attribute
{
    public LeaseFilterExemptAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>Why this action is exempt from lease filtering. Required, for auditability.</summary>
    public string Reason { get; }
}
