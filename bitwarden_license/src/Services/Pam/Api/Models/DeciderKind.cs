namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// What produced a decision on an access request, as it appears on the wire: <c>0 = automatic</c>, <c>1 = human</c>.
/// </summary>
/// <remarks>
/// Scaffold only: a standalone copy of the domain decider kind so the DTOs carry the wire contract without coupling to
/// the PAM domain. The real type lands with the rest of the PAM feature.
/// </remarks>
public enum DeciderKind : byte
{
    /// <summary>An access rule decided automatically, with no human approver.</summary>
    Automatic = 0,

    /// <summary>A human approver made the decision.</summary>
    Human = 1,
}
