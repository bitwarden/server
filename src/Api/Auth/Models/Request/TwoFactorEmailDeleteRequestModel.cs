using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request model for deleting a user's Email two-factor configuration.</summary>
public class TwoFactorEmailDeleteRequestModel
{
    /// <summary>Token minted by <c>GetEmail</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
