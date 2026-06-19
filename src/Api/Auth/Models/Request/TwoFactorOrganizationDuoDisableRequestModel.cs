using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request body for <c>DELETE /organizations/{id}/two-factor/duo</c>.</summary>
public class TwoFactorOrganizationDuoDisableRequestModel
{
    /// <summary>Token minted by <c>GetOrganizationDuo</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
