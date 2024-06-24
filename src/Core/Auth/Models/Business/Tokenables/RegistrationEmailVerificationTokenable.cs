using System.Text.Json.Serialization;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

// <summary>
// This token contains encrypted registration information for new users. The token is sent via email for verification as
// part of a link to complete the registration process.
// </summary>
public class RegistrationEmailVerificationTokenable : ExpiringTokenable
{
    public static TimeSpan GetTokenLifetime() => TimeSpan.FromMinutes(15);

    public const string ClearTextPrefix = "BwRegistrationEmailVerificationToken_";
    public const string DataProtectorPurpose = "RegistrationEmailVerificationTokenDataProtector";
    public const string TokenIdentifier = "RegistrationEmailVerificationToken";

    public string Identifier { get; set; } = TokenIdentifier;

    public string Name { get; set; }
    public string Email { get; set; }
    public bool ReceiveMarketingEmails { get; set; }

    [JsonConstructor]
    public RegistrationEmailVerificationTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(GetTokenLifetime());
    }

    public RegistrationEmailVerificationTokenable(string email, string name = default, bool receiveMarketingEmails = default) : this()
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentNullException(nameof(email));
        }

        Email = email;
        Name = name;
        ReceiveMarketingEmails = receiveMarketingEmails;
    }

    public bool TokenIsValid(string email, string name = default, bool receiveMarketingEmails = default)
    {
        if (Email == default || email == default)
        {
            return false;
        }

        // Note: string.Equals handles nulls without throwing an exception
        return string.Equals(Name, name, StringComparison.InvariantCultureIgnoreCase) &&
               Email.Equals(email, StringComparison.InvariantCultureIgnoreCase) &&
               ReceiveMarketingEmails == receiveMarketingEmails;
    }

    // Validates deserialized
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier
        && !string.IsNullOrWhiteSpace(Email);

}
