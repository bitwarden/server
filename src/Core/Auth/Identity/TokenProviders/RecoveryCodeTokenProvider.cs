using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.Identity.TokenProviders;

/// <summary>
/// We are repurposing the TwoFactorTokenProvider workflow to handle Recovery Codes
/// being sent in because it is the easiest way identified to handle allowing a user
/// to successfully go through the login process while providing their Recovery Code.
///
/// Originally, submitting your recovery code would land you on the login screen,
/// but with the approach of using a TwoFactorTokenProvider we don't have to embed
/// logic jankily in other parts of the recovery code process to get them logged in,
/// we can treat Recovery Codes as just another 2FA method. This means that some of
/// the functionality will not be needed in the same way it's needed for other 2FA
/// methods.
/// </summary>
public class RecoveryCodeTokenProvider : IUserTwoFactorTokenProvider<User>
{
    public virtual Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
    {
        return Task.FromResult(!string.IsNullOrEmpty(user.TwoFactorRecoveryCode));
    }

    /// <summary>
    /// This function shouldn't get called because we are not using recovery codes
    /// the typical 2FA flow.
    /// </summary>
    public virtual async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        // What is a better way to build a response to exit out of this?
        throw new Exception("This should not have been called.");
    }

    public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        // Is there a proper way (cryptographic approach) to prep the token to be compared?
        var processedToken = token.Replace(" ", string.Empty).ToLower();
        return Task.FromResult(string.Equals(processedToken, user.TwoFactorRecoveryCode));
    }
}
