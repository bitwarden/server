#nullable enable
namespace Bit.Core.Auth.UserFeatures.Registration;

public interface ISendVerificationEmailForRegistrationCommand
{
    /// <summary>
    /// Starts the email-verified registration flow; sends a verification email only when the
    /// email doesn't already belong to an account.
    /// </summary>
    /// <param name="sealedOpenOrgInviteData">
    /// Optional opaque SDK-produced blob. Echoed to the verification email URL on the new-user
    /// branch; dropped on the existing-user branch (anti-enumeration).
    /// </param>
    public Task<string?> Run(string email, string? name, bool receiveMarketingEmails, string? fromMarketing,
        string? sealedOpenOrgInviteData = null);
}
