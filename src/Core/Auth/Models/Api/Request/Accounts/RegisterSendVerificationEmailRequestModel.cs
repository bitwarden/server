#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Attributes;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterSendVerificationEmailRequestModel
{
    [StringLength(50)] public string? Name { get; set; }
    [StrictEmailAddress]
    [StringLength(256)]
    public required string Email { get; set; }
    public bool ReceiveMarketingEmails { get; set; }
    [MarketingInitiativeValidation]
    public string? FromMarketing { get; set; }

    /// <summary>
    /// Opaque wire artifact produced by the SDK on registration-start when the registrant is
    /// completing an open organization invite. Rides the verification email URL fragment so the
    /// verification-email tab can unseal it after auto-login. The server never parses this value
    /// — it is echoed through to the email URL and discarded.
    /// </summary>
    public string? SealedOpenOrgInviteData { get; set; }
}
