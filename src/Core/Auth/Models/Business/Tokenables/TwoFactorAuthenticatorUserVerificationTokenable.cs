using Bit.Core.Entities;
using Bit.Core.Tokens;
using Newtonsoft.Json;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>
/// A tokenable object that gives a user the ability to update their authenticator two factor settings.
/// </summary>
/// <remarks>
/// We protect two factor updates behind user verification (re-authentication) to protect against attacks of opportunity
/// (e.g. a user leaves their web vault unlocked). Most two factor options only require user verification (UV) when
/// enabling or disabling the option, retrieving the current status usually isn't a sensitive operation. However,
/// the status of authenticator two factor is sensitive because it reveals the user's secret key, which means both
/// operations must be protected by UV.
///
/// TOTP as a UV option is only allowed to be used once, so we return this tokenable when retrieving the current status
/// (and secret key) of authenticator two factor to give the user a means of passing UV when updating (enabling/disabling).
/// </remarks>
public class TwoFactorAuthenticatorUserVerificationTokenable : ExpiringTokenable
{
    private static readonly TimeSpan _tokenLifetime = TimeSpan.FromMinutes(30);

    public const string ClearTextPrefix = "TwoFactorAuthenticatorUserVerification";
    public const string DataProtectorPurpose = "TwoFactorAuthenticatorUserVerificationTokenDataProtector";
    public const string TokenIdentifier = "TwoFactorAuthenticatorUserVerificationToken";
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid UserId { get; set; }
    public string Key { get; set; }

    public override bool Valid => Identifier == TokenIdentifier &&
                                  UserId != default;

    [JsonConstructor]
    public TwoFactorAuthenticatorUserVerificationTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(_tokenLifetime);
    }

    public TwoFactorAuthenticatorUserVerificationTokenable(User user, string key) : this()
    {
        UserId = user?.Id ?? default;
        Key = key;
    }

    public bool TokenIsValid(User user, string key)
    {
        if (UserId == default
            || user == null
            || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return UserId == user.Id && Key == key;
    }

    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier
        && UserId != default
        && !string.IsNullOrWhiteSpace(Key);
}
