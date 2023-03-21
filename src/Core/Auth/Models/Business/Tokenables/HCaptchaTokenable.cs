using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables;

public class HCaptchaTokenable : ExpiringTokenable
{
    private const double _tokenLifetimeInHours = (double)5 / 60; // 5 minutes
    public const string ClearTextPrefix = "BWCaptchaBypass_";
    public const string DataProtectorPurpose = "CaptchaServiceDataProtector";
    public const string TokenIdentifier = "CaptchaBypassToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }
    public string Email { get; set; }

    [JsonConstructor]
    public HCaptchaTokenable()
    {
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
    }

    public HCaptchaTokenable(User user) : this()
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
