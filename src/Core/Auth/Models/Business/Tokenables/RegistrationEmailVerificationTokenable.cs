// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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

    /// <summary>
    /// True when this token was delivered by email, so presenting it proves ownership of <see cref="Email"/>.
    /// False when it was returned directly to the caller without an email being sent.
    /// Null for tokens issued before this property existed.
    /// </summary>
    // TODO: PM-45479 - Make this a non-nullable bool and drop the null case from the doc comment.
    public bool? EmailSent { get; set; }

    [JsonConstructor]
    public RegistrationEmailVerificationTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(GetTokenLifetime());
    }

    public RegistrationEmailVerificationTokenable(string email, string name = default, bool receiveMarketingEmails = default,
        bool emailSent = false) : this()
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentNullException(nameof(email));
        }

        Email = email;
        Name = name;
        ReceiveMarketingEmails = receiveMarketingEmails;
        EmailSent = emailSent;
    }

    public bool TokenIsValid(string email)
    {
        if (Email == default || email == default)
        {
            return false;
        }

        return Email.Equals(email, StringComparison.InvariantCultureIgnoreCase);
    }

    // Validates deserialized
    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier
        && !string.IsNullOrWhiteSpace(Email);


    public static bool ValidateToken(IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> dataProtectorTokenFactory, string token, string userEmail)
    {
        return dataProtectorTokenFactory.TryUnprotect(token, out var tokenable)
               && tokenable.Valid
               && tokenable.TokenIsValid(userEmail);
    }


}
