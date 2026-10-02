#nullable enable
using Bit.Core.Auth.Models.Api.Request.Accounts;

namespace Bit.Core.Auth.UserFeatures.Registration;

public interface ISendVerificationEmailForRegistrationCommand
{
    /// <summary>
    /// Starts the email-verified registration flow; sends a verification email only when the
    /// email doesn't already belong to an account.
    /// </summary>
    /// <param name="openOrgInvite">
    /// Optional open-org-invite payload. When present, the sealed blob is echoed to the
    /// verification email URL on the new-user branch (dropped on the existing-user branch for
    /// anti-enumeration), and the invite's organization is excluded from the claimed-domain
    /// block check so a user reaching registration via that org's link can proceed with a
    /// domain claimed by that org.
    /// </param>
    public Task<string?> Run(string email, string? name, bool receiveMarketingEmails, string? fromMarketing,
        RegisterStartOpenOrgInviteRequestModel? openOrgInvite = null);
}
