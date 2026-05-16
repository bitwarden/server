using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.UserEmail;

public interface IChangeEmailCommand
{
    /// <summary>
    /// Updates the user's email after all upstream authentication and authorization checks have
    /// passed. Throws <see cref="BadRequestException"/> if <paramref name="newEmail"/> is already
    /// in use or violates an organization's claimed-domain policy. Rolls back if the downstream
    /// Stripe customer email sync fails. On success, logs the user out of all sessions (if they
    /// have a master password) or signals a settings sync (if they do not).
    /// </summary>
    Task ChangeEmailAsync(User user, string newEmail);
}
