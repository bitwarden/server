using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request model for deleting a user's YubiKey two-factor configuration.</summary>
public class TwoFactorYubiKeyDeleteRequestModel
{
    /// <summary>Token minted by <c>GetYubiKey</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
