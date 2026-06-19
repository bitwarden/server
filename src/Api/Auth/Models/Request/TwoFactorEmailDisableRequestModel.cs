using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request body for <c>DELETE /two-factor/email</c>.</summary>
public class TwoFactorEmailDisableRequestModel
{
    /// <summary>Token minted by <c>GetEmail</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
