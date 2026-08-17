namespace Bit.Services.Pam.Api.Models;

/// <summary>
/// What produced a decision on an access request, as it appears on the wire: <c>0 = automatic</c>, <c>1 = human</c>.
/// </summary>
/// <remarks>
/// A standalone copy of the domain decider kind so the DTOs carry the wire contract without coupling to the PAM
/// domain; <see cref="DomainEnumMapping"/> converts between the two.
/// </remarks>
public enum DeciderKind : byte
{
    /// <summary>An access rule decided automatically, with no human approver.</summary>
    Automatic = 0,

    /// <summary>A human approver made the decision.</summary>
    Human = 1,
}
