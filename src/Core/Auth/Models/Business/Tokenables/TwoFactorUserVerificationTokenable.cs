using System.Text.Json.Serialization;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>
/// Single-use proof that a user passed entry-level secret verification (master password or OTP).
/// Issued by per-provider GET endpoints (e.g. <c>GetYubiKey</c>) and replayed on the subsequent
/// PUT / DELETE so the user does not have to re-verify a second time within the token lifetime.
/// Bound to <see cref="UserId"/> + <see cref="ProviderType"/> to prevent cross-provider replay.
/// </summary>
public class TwoFactorUserVerificationTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "TwoFactorUserVerification_";
    public const string DataProtectorPurpose = "TwoFactorUserVerificationTokenDataProtector";
    public const string TokenIdentifier = "TwoFactorUserVerificationToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid UserId { get; set; }
    public TwoFactorProviderType ProviderType { get; set; }

    /// <summary>
    /// Required for <c>JsonSerializer.Deserialize&lt;T&gt;</c> inside <c>DataProtectorTokenFactory.Unprotect</c>,
    /// which deserializes the decrypted JSON into this type. Production code must mint via
    /// <see cref="TwoFactorUserVerificationTokenableFactory"/> so the
    /// <c>IGlobalSettings.TwoFactorUserVerificationTokenLifetimeInMinutes</c> value applies — a
    /// direct <c>new()</c> yields <c>ExpirationDate == default</c> and fails <see cref="Tokenable.Valid"/>.
    /// </summary>
    [JsonConstructor]
    public TwoFactorUserVerificationTokenable() { }

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
