using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public interface IMasterPasswordHasher
{
    /// <summary>
    /// Validates the client-side hash meets password requirements, then produces
    /// the server-side hash. Returns failure result if validation fails.
    /// </summary>
    Task<(IdentityResult Result, string? ServerSideHash)> ValidateAndHashPasswordAsync(
        User user, string clientSideHash);

    /// <summary>
    /// Produces the server-side hash without validation. Use for rehash-on-login
    /// or admin recovery scenarios where validation is not applicable.
    /// </summary>
    string HashPassword(User user, string clientSideHash);
}
