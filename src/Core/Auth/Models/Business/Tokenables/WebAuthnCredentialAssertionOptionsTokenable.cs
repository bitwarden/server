using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class WebAuthnCredentialAssertionOptionsTokenable : ExpiringTokenable
{
    // 7 minutes = max webauthn timeout (6 minutes) + slack for miscellaneous delays
    private const double _tokenLifetimeInHours = (double)7 / 60;
    public const string ClearTextPrefix = "BWWebAuthnCredentialAssertionOptions_";
    public const string DataProtectorPurpose = "WebAuthnCredentialAssertionDataProtector";
    public const string TokenIdentifier = "WebAuthnCredentialAssertionOptionsToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid? UserId { get; set; }
    public AssertionOptions Options { get; set; }

    [JsonConstructor]
    public WebAuthnCredentialAssertionOptionsTokenable()
    {
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
    }

    public WebAuthnCredentialAssertionOptionsTokenable(User user, AssertionOptions options) : this()
    {
        UserId = user?.Id;
        Options = options;
    }

    public bool TokenIsValid(User user)
    {
        if (!Valid || user == null)
        {
            return false;
        }

        return UserId == user.Id;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && UserId != null && Options != null;
}

