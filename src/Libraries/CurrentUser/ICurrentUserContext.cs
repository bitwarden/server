namespace Bit.CurrentUser;

/// <summary>
/// The authenticated user of the current HTTP request, read from the bearer token's claims.
/// Holds only what the token asserts. Nothing here touches a repository.
/// </summary>
/// <remarks>
/// Both members read the <c>sub</c> claim as-is. Non-user tokens, such as service-account and Send
/// access tokens, also carry <c>sub</c>. What these members return for those tokens is unspecified here,
/// so consume this interface only behind a policy that requires a user.
/// </remarks>
public interface ICurrentUserContext
{
    /// <summary>
    /// The caller's user id from the token's <c>sub</c> claim.
    /// Throws <see cref="InvalidOperationException"/> when no HTTP request is in scope or the
    /// principal carries no valid user id. Consume only behind a policy that requires a user.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Reads the caller's user id from the token's <c>sub</c> claim without throwing.
    /// Returns <see langword="false"/> when no HTTP request is in scope or the principal carries
    /// no valid user id, as in a background job. Use where the user is optional.
    /// A <see langword="false"/> result is not an authorization decision; enforce access with a policy.
    /// </summary>
    /// <param name="userId">The caller's user id when found; otherwise <see cref="Guid.Empty"/>.</param>
    bool TryGetUserId(out Guid userId);
}
