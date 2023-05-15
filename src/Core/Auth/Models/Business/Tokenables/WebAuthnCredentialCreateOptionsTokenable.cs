using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class WebAuthnCredentialCreateOptionsTokenable : ExpiringTokenable
{
    // 7 minutes = max webauthn timeout (6 minutes) + slack for miscellaneous delays
    private const double _tokenLifetimeInHours = (double)7 / 60;
    public const string ClearTextPrefix = "BWWebAuthnCredentialCreateOptions_";
    public const string DataProtectorPurpose = "WebAuthnCredentialCreateDataProtector";
    public const string TokenIdentifier = "WebAuthnCredentialCreateOptionsToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public Guid? UserId { get; set; }
    public CredentialCreateOptions Options { get; set; }

    [JsonConstructor]
    public WebAuthnCredentialCreateOptionsTokenable()
    {
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
    }

    public WebAuthnCredentialCreateOptionsTokenable(User user, CredentialCreateOptions options) : this()
    {
        UserId = user?.Id;
        Options = options;
    }

    public bool TokenIsValid(User user)
    {
        if (!Valid || user == default)
        {
            return false;
        }

        return UserId == user.Id;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && UserId != null && Options != default;
}

