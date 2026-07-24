#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Attributes;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterSendVerificationEmailRequestModel
{
    // Bounds the anonymous request body; also caps the derived TrialSendVerificationEmailRequestModel.
    private const int SealedOpenOrgInviteDataMaxLength = 4096;

    [StringLength(50)] public string? Name { get; set; }
    [StrictEmailAddress]
    [StringLength(256)]
    public required string Email { get; set; }
    public bool ReceiveMarketingEmails { get; set; }
    [MarketingInitiativeValidation]
    public string? FromMarketing { get; set; }

    /// <summary>
    /// Opaque SDK-produced blob for open-org-invite registrations. Echoed to the verification
    /// email URL; never parsed server-side.
    /// </summary>
    [MaxLength(SealedOpenOrgInviteDataMaxLength)]
    public string? SealedOpenOrgInviteData { get; set; }
}
