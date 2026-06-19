using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request body for <c>DELETE /two-factor/webauthn/all</c>.</summary>
public class TwoFactorWebAuthnDeleteAllRequestModel
{
    /// <summary>Token minted by <c>GetWebAuthn</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
