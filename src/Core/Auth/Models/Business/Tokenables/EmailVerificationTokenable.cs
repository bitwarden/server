using System.Text.Json.Serialization;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.Models.Business.Tokenables;

// <summary>
// This token contains encrypted registration information for new users. The token is sent via email for verification as
// part of a link to complete the registration process.
// </summary>
public class EmailVerificationTokenable : ExpiringTokenable
{
    // TODO: ask what the token lifetime should be
    public static TimeSpan GetTokenLifetime() => TimeSpan.FromDays(5);

    public const string ClearTextPrefix = "BwEmailVerificationToken_";
    public const string DataProtectorPurpose = "EmailVerificationTokenDataProtector";
    public const string TokenIdentifier = "EmailVerificationToken";

    public string Identifier { get; set; } = TokenIdentifier;

    public string Name { get; set; }
    public string Email { get; set; }
    public bool ReceiveMarketingEmails { get; set; }

    [JsonConstructor]
    public EmailVerificationTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(GetTokenLifetime());
    }

    public EmailVerificationTokenable(string email, string name = default, bool receiveMarketingEmails = default) : this()
    {
        Name = name;
        Email = email;
        ReceiveMarketingEmails = receiveMarketingEmails;
    }

    public bool TokenIsValid(string email, string name = default, bool receiveMarketingEmails = default)
    {
        if (Email == default || email == default)
        {
            return false;
        }

        return Name.Equals(name, StringComparison.InvariantCultureIgnoreCase) &&
               Email.Equals(email, StringComparison.InvariantCultureIgnoreCase) &&
               ReceiveMarketingEmails == receiveMarketingEmails;
    }

    // Validates deserialized
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier
        && !string.IsNullOrWhiteSpace(Email);

}
