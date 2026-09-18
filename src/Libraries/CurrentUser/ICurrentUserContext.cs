namespace Bit.CurrentUser;

/// <summary>
/// The authenticated user of the current HTTP request, read from the bearer token's claims.
/// Holds only what the token asserts. Nothing here touches a repository.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// The caller's user id from the token's <c>sub</c> claim.
    /// Throws <see cref="InvalidOperationException"/> when no HTTP request is in scope or the
    /// principal carries no valid user id. Consume only behind a policy that requires a user.
    /// </summary>
    Guid UserId { get; }
}
