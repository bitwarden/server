using Bit.Core.Entities;
using Bit.Core.Tokens;
using Newtonsoft.Json;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class TwoFactorAuthenticatorUserVerificationTokenable : ExpiringTokenable
{
    private static readonly TimeSpan _tokenLifetime = TimeSpan.FromMinutes(5);

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
