using Bit.Core.Entities;
namespace Bit.Core.Auth.UserFeatures.UserEmail;

public interface IChangeEmailCommand
{
    /// <summary>
    /// Updates the user's email after all upstream authentication and authorization checks have
    /// passed. On success, logs the user out of all sessions (if they
    /// have a master password) or signals a settings sync (if they do not).
    /// </summary>
    /// <remarks>
    /// This command performs no identity or ownership verification of the new email. Callers MUST
    /// perform robust verification before invoking it, including (at minimum):
    /// <list type="bullet">
    /// <item><description>Authenticating the requesting user and confirming they own
    /// <paramref name="user"/>.</description></item>
    /// <item><description>Verifying control of <paramref name="newEmail"/> (e.g. token/OTP
    /// challenge sent to the new address).</description></item>
    /// </list>
    /// Invoking this command without those checks may allow a user's email to be changed to an
    /// address they do not control.
    /// <para>
    /// As defense-in-depth, this command also enforces the organization-domain policy for users
    /// claimed by an organization with verified domains, mirroring the gate in
    /// <c>RegisterUserCommand</c> so the policy cannot be bypassed via email change. Callers may
    /// still perform their own domain check ahead of time for better UX or context-specific error
    /// handling, but they are not required to.
    /// </para>
    /// </remarks>
    /// <param name="user">The user whose email is being changed.</param>
    /// <param name="newEmail">The fully verified new email address.</param>
    Task ChangeEmailAsync(User user, string newEmail);
}
