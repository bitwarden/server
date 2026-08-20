namespace Bit.Services.Pam.Errors;

/// <summary>
/// The property names PAM errors are keyed by in an RFC 7807 problem response.
/// </summary>
/// <remarks>
/// A PAM error names the request property a client can correct — the same name the request model serializes it
/// under, so a form can mark that control invalid without a translation table. A failure that is about the request
/// as a whole rather than one of its fields is keyed by <see cref="Code"/> instead.
/// </remarks>
public static class PamErrorProperties
{
    /// <summary>
    /// The key for a failure no single field caused: a state conflict, a request shaped for the wrong approval
    /// mode, or a denial by the governing rule. There is nothing to mark invalid — the client reads the code and
    /// decides what to do.
    /// </summary>
    public const string Code = "code";
}
