using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request;

/// <summary>Request model for deleting a user's YubiKey two-factor configuration.</summary>
public class TwoFactorYubiKeyDeleteRequestModel
{
    /// <summary>
    /// User-verification token bound to <c>UserId + ProviderType</c>. Minted by the matching GET
    /// endpoint and replayed on subsequent management calls so the user does not have to re-verify.
    /// </summary>
    [Required]
    public string UserVerificationToken { get; set; } = null!;
}
