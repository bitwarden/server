using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request model for deleting an organization's Duo two-factor configuration.</summary>
public class TwoFactorOrganizationDuoDeleteRequestModel
{
    /// <summary>Token minted by <c>GetOrganizationDuo</c>; bound to <c>UserId + ProviderType</c>.</summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
