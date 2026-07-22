#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Attributes;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterSendVerificationEmailRequestModel
{
    // Cap covers the CBOR + base64url overhead over a DataEnvelope +
    // SecretProtectedKeyEnvelope pair produced by the SDK's `seal_open_org_invite_data` (see
    // PM-40520). Measured length for a realistic invite payload is ~1202 chars; 4096 gives
    // ~3.4x headroom for future schema evolution while still tightly bounding an anonymous
    // request body from a spam / abuse perspective.
    private const int SealedOpenOrgInviteDataMaxLength = 4096;

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
    [MaxLength(SealedOpenOrgInviteDataMaxLength)]
    public string? SealedOpenOrgInviteData { get; set; }
}
