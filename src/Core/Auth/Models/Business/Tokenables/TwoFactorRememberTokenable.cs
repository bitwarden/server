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
/// device's <c>TwoFactorRememberToken</c> row.
/// </remarks>
public class TwoFactorRememberTokenable : ExpiringTokenable
{
    public static TimeSpan GetTokenLifetime() => TimeSpan.FromDays(30);

    public const string ClearTextPrefix = "BwTwoFactorRemember_";
    public const string DataProtectorPurpose = "TwoFactorRememberTokenDataProtector";
    public const string TokenIdentifier = "TwoFactorRememberToken";

    public string Identifier { get; set; } = TokenIdentifier;

    public Guid UserId { get; set; }

    /// <summary>
    /// The primary key of the device this token was issued to, used to locate the row to compare
    /// <see cref="Stamp"/> against.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// The client-generated device identifier from the request that minted this token. Compared
    /// against the presenting request's identifier, which binds the token to one device without
    /// needing a database read.
    /// </summary>
    public string DeviceIdentifier { get; set; } = null!;

    /// <summary>
    /// A copy of the issuing device's row stamp. Device-scoped.
    /// </summary>
    public string Stamp { get; set; } = null!;

    // TODO: PM-XXXXX - Consider removing SecurityStamp from this token. The row's Stamp already
    // covers the same ground more precisely, and carrying the user stamp ties remember-me lifetime
    // to every user stamp rotation. Any removal has to stay backwards compatible: tokens already
    // issued carry this field, so treat it as optional on read before dropping it.
    /// <summary>
    /// A copy of <c>User.SecurityStamp</c> as it stood at issuance. Account-scoped.
    /// </summary>
    public string SecurityStamp { get; set; } = null!;

    [JsonConstructor]
    public TwoFactorRememberTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(GetTokenLifetime());
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
}
