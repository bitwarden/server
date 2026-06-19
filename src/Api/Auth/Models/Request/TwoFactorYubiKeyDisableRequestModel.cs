using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request body for <c>DELETE /two-factor/yubikey</c>.</summary>
public class TwoFactorYubiKeyDisableRequestModel
{
    /// <summary>Token minted by <c>GetYubiKey</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
