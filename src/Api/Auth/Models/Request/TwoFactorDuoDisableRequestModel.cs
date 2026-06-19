using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request body for <c>DELETE /two-factor/duo</c>.</summary>
public class TwoFactorDuoDisableRequestModel
{
    /// <summary>Token minted by <c>GetDuo</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
