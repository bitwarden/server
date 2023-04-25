using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

// This token just provides a verifiable authN mechanism for the API service
// TwoFactorController.cs SendEmailLogin anonymous endpoint so it cannot be
// used maliciously. 
public class SsoEmail2faSessionTokenable : ExpiringTokenable
{
    // Just over 2 min expiration (client expires session after 2 min)
    private const double _tokenLifetimeInHours = (double)2.05 / 60;
    public const string ClearTextPrefix = "BwSsoEmail2FaSessionToken_";
    public const string DataProtectorPurpose = "SsoEmail2faSessionTokenDataProtector";

    public const string TokenIdentifier = "SsoEmail2faSessionToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }
    public string Email { get; set; }


    [JsonConstructor]
    public SsoEmail2faSessionTokenable()
    {
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
    }

    public SsoEmail2faSessionTokenable(User user) : this()
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
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && Id != default && !string.IsNullOrWhiteSpace(Email);
}
