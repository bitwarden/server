using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request model for deleting a user's Duo two-factor configuration.</summary>
public class TwoFactorDuoDeleteRequestModel
{
    /// <summary>Token minted by <c>GetDuo</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
