using System.Text.Json.Serialization;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>
/// This token authorizes a sales-assisted registration. It carries the prospect's email (and optional name)
/// and is sent as part of a link to complete registration. Unlike the self-service registration token,
/// it deliberately omits a marketing email opt-in.
/// Use when: registering new users in any environment where open enrollment is disabled.
/// </summary>
public class SalesAssistedRegistrationTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "BwSalesAssistedRegistrationToken_";
    public const string DataProtectorPurpose = "SalesAssistedRegistrationTokenDataProtector";
    public const string TokenIdentifier = "SalesAssistedRegistrationToken";

    // Binding properties use [JsonInclude] internal set so only the factory (in Bit.Core) can mint a token.
    // [JsonInclude] is required: JsonSerializer with default options ignores non-public setters, and
    // deserialized tokens would silently come back with default values.
    [JsonInclude]
    public string Identifier { get; internal set; } = TokenIdentifier;
    [JsonInclude]
    public string Email { get; internal set; } = null!;
    [JsonInclude]
    public string? Name { get; internal set; }

    [JsonConstructor]
    public SalesAssistedRegistrationTokenable()
    {
    }

    internal SalesAssistedRegistrationTokenable(string email, string? name)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        }

        Identifier = TokenIdentifier;
        Email = email;
        Name = name;
    }

    public bool TokenIsValid(string email) =>
        TokenIsValid() && string.Equals(Email, email, StringComparison.InvariantCultureIgnoreCase);

    // Validates deserialized
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && !string.IsNullOrWhiteSpace(Email);

    public static TokenableValidationError? ValidateSalesAssistedRegistrationToken(
        IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable> salesAssistedRegistrationTokenDataFactory,
        string token,
        string email) =>
        salesAssistedRegistrationTokenDataFactory.TryUnprotect(token, out var decryptedToken) switch
        {
            true when decryptedToken.IsExpired => TokenableValidationError.ExpiringTokenables.Expired,
            true when !(decryptedToken.Valid && decryptedToken.TokenIsValid(email)) =>
                TokenableValidationError.InvalidToken,
            false => TokenableValidationError.InvalidToken,
            _ => null
        };
}
