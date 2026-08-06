using System.Text.Json.Serialization;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>
/// Time-limited proof that a user passed entry-level secret verification (master password or OTP).
/// Issued by per-provider GET endpoints (e.g. <c>GetYubiKey</c>) and replayed on the subsequent
/// PUT / DELETE within the token's lifetime so the user does not need to re-verify. Bound to
/// <see cref="UserId"/> + <see cref="ProviderType"/> to prevent cross-provider replay.
/// </summary>
public class TwoFactorUserVerificationTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "TwoFactorUserVerification_";
    public const string DataProtectorPurpose = "TwoFactorUserVerificationTokenDataProtector";
    public const string TokenIdentifier = "TwoFactorUserVerificationToken";

    // Binding properties use [JsonInclude] internal set so only the factory (in
    // Bit.Core) can mint a token. [JsonInclude] is required: JsonSerializer with
    // default options ignores non-public setters, and deserialized tokens would
    // silently come back with default values.
    [JsonInclude]
    public string Identifier { get; internal set; } = TokenIdentifier;
    [JsonInclude]
    public Guid UserId { get; internal set; }
    [JsonInclude]
    public TwoFactorProviderType ProviderType { get; internal set; }

    [JsonConstructor]
    public TwoFactorUserVerificationTokenable() { }

    internal TwoFactorUserVerificationTokenable(User user, TwoFactorProviderType providerType, TimeSpan lifetime)
    {
        UserId = user.Id;
        ProviderType = providerType;
        ExpirationDate = DateTime.UtcNow.Add(lifetime);
    }

    /// <summary>Returns true iff this token was minted for the given user and provider.</summary>
    public bool TokenIsValid(User user, TwoFactorProviderType providerType) =>
        user is not null
        && UserId != default
        && UserId == user.Id
        && ProviderType == providerType;

    /// <inheritdoc />
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && UserId != default;

    /// <summary>
    /// Unprotect, validate expiry + identifier, and verify the user/provider binding in one call.
    /// </summary>
    public static bool Validate(
        IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable> factory,
        string token,
        User user,
        TwoFactorProviderType providerType) =>
        factory.TryUnprotect(token, out var decryptedToken)
        && decryptedToken.Valid
        && decryptedToken.TokenIsValid(user, providerType);
}
