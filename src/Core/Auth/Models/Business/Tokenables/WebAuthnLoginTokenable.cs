using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class WebAuthnLoginTokenable : ExpiringTokenable
{
    private const double _tokenLifetimeInHours = (double)1 / 60; // 1 minute
    public const string ClearTextPrefix = "BWWebAuthnLogin_";
    public const string DataProtectorPurpose = "WebAuthnLoginDataProtector";
    public const string TokenIdentifier = "WebAuthnLoginToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }
    public string Email { get; set; }

    [JsonConstructor]
    public WebAuthnLoginTokenable()
    {
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
    }

    public WebAuthnLoginTokenable(User user) : this()
    {
        Id = user?.Id ?? default;
        Email = user?.Email;
    }

    public bool TokenIsValid(User user)
    {
        if (Id == default || Email == default || user == null)
        {
            return false;
        }

        return Id == user.Id &&
        Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase);
    }

    // Validates deserialized 
    protected override bool TokenIsValid() => Identifier == TokenIdentifier && Id != default && !string.IsNullOrWhiteSpace(Email);
}
