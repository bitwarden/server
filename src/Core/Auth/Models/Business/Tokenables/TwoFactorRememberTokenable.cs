using System.Text.Json.Serialization;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>
/// The "remember this device" token a client presents to satisfy two-factor authentication without
/// being challenged again.
/// </summary>
/// <remarks>
/// The token is encrypted and signed, so a client can neither read nor alter its contents. It names
/// the device it was issued to and carries two stamps that are compared against server-side state on
/// every use: <see cref="SecurityStamp"/> against the user, and <see cref="Stamp"/> against that
/// device's <c>TwoFactorRememberToken</c> row. Minted only through
/// <see cref="ITwoFactorRememberTokenableFactory"/>, which applies the configured lifetime.
/// </remarks>
public class TwoFactorRememberTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "BwTwoFactorRemember_";
    public const string DataProtectorPurpose = "TwoFactorRememberTokenDataProtector";
    public const string TokenIdentifier = "TwoFactorRememberToken";

    // Binding properties use [JsonInclude] internal set so only the factory (in Bit.Core) can mint a
    // token. [JsonInclude] is required: JsonSerializer with default options ignores non-public
    // setters, and deserialized tokens would silently come back with default values.
    [JsonInclude]
    public string Identifier { get; internal set; } = TokenIdentifier;

    [JsonInclude]
    public Guid UserId { get; internal set; }

    /// <summary>
    /// The primary key of the device this token was issued to, used to locate the row to compare
    /// <see cref="Stamp"/> against.
    /// </summary>
    [JsonInclude]
    public Guid DeviceId { get; internal set; }

    /// <summary>
    /// The client-generated device identifier from the request that minted this token. Compared
    /// against the presenting request's identifier, which binds the token to one device without
    /// needing a database read.
    /// </summary>
    [JsonInclude]
    public string DeviceIdentifier { get; internal set; } = null!;

    /// <summary>
    /// A copy of the issuing device's row stamp. Device-scoped.
    /// </summary>
    [JsonInclude]
    public string Stamp { get; internal set; } = null!;

    // TODO: PM-44171 - Consider removing SecurityStamp from this token. The row's Stamp already
    // covers the same ground more precisely, and carrying the user stamp ties remember-me lifetime
    // to every user stamp rotation. Any removal has to stay backwards compatible: tokens already
    // issued carry this field, so treat it as optional on read before dropping it.
    /// <summary>
    /// A copy of <c>User.SecurityStamp</c> as it stood at issuance. Account-scoped.
    /// </summary>
    [JsonInclude]
    public string SecurityStamp { get; internal set; } = null!;

    // Leaves ExpirationDate at its default, so a token constructed without going through the factory
    // is expired and therefore never valid.
    [JsonConstructor]
    public TwoFactorRememberTokenable()
    {
    }

    internal TwoFactorRememberTokenable(
        Guid userId, Guid deviceId, string deviceIdentifier, string stamp, string securityStamp)
    {
        if (userId == default)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (deviceId == default)
        {
            throw new ArgumentException("Device id cannot be empty.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(deviceIdentifier))
        {
            throw new ArgumentException("Device identifier cannot be null or whitespace.", nameof(deviceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(stamp))
        {
            throw new ArgumentException("Stamp cannot be null or whitespace.", nameof(stamp));
        }

        if (string.IsNullOrWhiteSpace(securityStamp))
        {
            throw new ArgumentException("Security stamp cannot be null or whitespace.", nameof(securityStamp));
        }

        Identifier = TokenIdentifier;
        UserId = userId;
        DeviceId = deviceId;
        DeviceIdentifier = deviceIdentifier;
        Stamp = stamp;
        SecurityStamp = securityStamp;
    }

    /// <summary>
    /// Checks only that the deserialized fields are present and well-formed. Comparing them against
    /// the user and the device's row requires state this type does not have, and happens in
    /// <c>ValidateTwoFactorRememberTokenQuery</c>.
    /// </summary>
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && UserId != default && DeviceId != default
        && !string.IsNullOrWhiteSpace(DeviceIdentifier)
        && !string.IsNullOrWhiteSpace(Stamp)
        && !string.IsNullOrWhiteSpace(SecurityStamp);

    /// <summary>
    /// Unprotects a presented token and checks everything that can be checked without server-side
    /// state: tampering, a wrong data-protection purpose, the token's own expiry, and the presence of
    /// its fields. The caller still compares the result against the user and the device's row.
    /// </summary>
    /// <param name="tokenable">The unprotected token, or null when it could not be unprotected.</param>
    public static TokenableValidationError? ValidateTwoFactorRememberToken(
        IDataProtectorTokenFactory<TwoFactorRememberTokenable> tokenFactory,
        string token,
        out TwoFactorRememberTokenable? tokenable)
    {
        if (!tokenFactory.TryUnprotect(token, out tokenable) || tokenable is null)
        {
            return TokenableValidationError.InvalidToken;
        }

        if (tokenable.IsExpired)
        {
            return TokenableValidationError.ExpiringTokenables.Expired;
        }

        return tokenable.Valid ? null : TokenableValidationError.InvalidToken;
    }
}
