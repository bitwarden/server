namespace Bit.Identity.Models;

public class BaseRequestValidationState
{
    /// <summary>
    /// Validation methods, by name, which have passed validation in the current context.
    /// </summary>
    public List<string> CompletedValidators { get; set; } = [];
    /// <summary>
    /// Whether the user has requested a Remember Me token for their current device.
    /// </summary>
    public bool RememberMeRequested { get; set; } = false;
    /// <summary>
    /// Whether the user has requested recovery of their 2FA methods using their one-time
    /// recovery code.
    /// </summary>
    /// <see cref="Bit.Core.Auth.Enums.TwoFactorProviderType"/>
    public bool TwoFactorRecoveryRequested { get; set; } = false;
}
