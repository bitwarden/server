#nullable enable
namespace Bit.Core.Auth.UserFeatures.Registration;

public interface ISendVerificationEmailForRegistrationCommand
{
    /// <summary>
    /// Kicks off the email-verified registration flow: validates the request, generates a
    /// <c>RegistrationEmailVerificationTokenable</c>, and (when the caller's email doesn't
    /// already have an account) sends the verification email.
    /// </summary>
    /// <param name="sealedOpenOrgInviteData">
    /// Optional opaque wire artifact produced by the SDK's <c>seal_open_org_invite_data</c>
    /// (PM-40520). When present, is echoed through to the verification-email URL fragment so the
    /// verification-email tab can unseal it after auto-login. The server never parses this value.
    /// Silently discarded on the existing-account anti-enumeration branch so that the response is
    /// indistinguishable from the new-account branch.
    /// </param>
    public Task<string?> Run(string email, string? name, bool receiveMarketingEmails, string? fromMarketing,
        string? sealedOpenOrgInviteData = null);
}
