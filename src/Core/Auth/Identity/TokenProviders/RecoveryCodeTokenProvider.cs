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
    /// <summary>
    /// Hijack the can generate two factor token to repurpose it to check
    /// if the user has a two factor recovery code on their account.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="user"></param>
    /// <returns></returns>
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
        throw new Exception("This should not have been called.");
    }

    public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        var processedToken = token.Replace(" ", string.Empty).ToLower();
        return Task.FromResult(string.Equals(processedToken, user.TwoFactorRecoveryCode));
    }
}
