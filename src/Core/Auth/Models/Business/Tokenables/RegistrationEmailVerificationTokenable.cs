#nullable enable

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

    public const string ClearTextPrefix = "BWRegistrationEmailVerification_";
    public const string DataProtectorPurpose = "RegistrationEmailVerificationDataProtector";
    public const string TokenIdentifier = "RegistrationEmailVerificationToken";
    public string Identifier { get; set; } = TokenIdentifier;

    [JsonConstructor]
    public RegistrationEmailVerificationTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(GetTokenLifetime());
        Email = string.Empty;
        Name = null;
        ReceiveMarketingEmails = false;
        TrialLengthInDays = 7;
    }

    public RegistrationEmailVerificationTokenable(string? email, string? name, bool receiveMarketingEmails, int trialLengthInDays = 7)
        : this()
    {
        if (email == null)
        {
            throw new ArgumentNullException(nameof(email));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be empty or whitespace.", nameof(email));
        }

        Email = email;
        Name = name;
        ReceiveMarketingEmails = receiveMarketingEmails;
        TrialLengthInDays = trialLengthInDays;
    }

    public string Email { get; set; }
    public string? Name { get; set; }
    public bool ReceiveMarketingEmails { get; set; }
    public int TrialLengthInDays { get; set; }

    public bool TokenIsValid(string email)
    {
        return Valid && Email.Equals(email, StringComparison.InvariantCultureIgnoreCase);
    }

    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier &&
        !string.IsNullOrWhiteSpace(Email);

    public static bool ValidateToken(
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> tokenDataFactory,
        string token,
        string email)
    {
        return tokenDataFactory.TryUnprotect(token, out var decryptedToken)
               && decryptedToken.Valid
               && decryptedToken.TokenIsValid(email);
    }
}
