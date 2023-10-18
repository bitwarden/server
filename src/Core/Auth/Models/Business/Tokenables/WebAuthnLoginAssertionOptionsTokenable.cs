using System.Text.Json.Serialization;
using Bit.Core.Auth.Enums;
using Bit.Core.Tokens;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class WebAuthnLoginAssertionOptionsTokenable : ExpiringTokenable
{
    // 7 minutes = max webauthn timeout (6 minutes) + slack for miscellaneous delays
    private const double _tokenLifetimeInHours = (double)7 / 60;
    public const string ClearTextPrefix = "BWWebAuthnLoginAssertionOptions_";
    public const string DataProtectorPurpose = "WebAuthnLoginAssetionOptionsDataProtector";
    public const string TokenIdentifier = "WebAuthnLoginAssertionOptionsToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public AssertionOptions Options { get; set; }
    public WebAuthnLoginAssertionOptionsScope Scope { get; set;  }

    [JsonConstructor]
    public WebAuthnLoginAssertionOptionsTokenable()
    {
        ExpirationDate = DateTime.UtcNow.AddHours(_tokenLifetimeInHours);
    }

    public WebAuthnLoginAssertionOptionsTokenable(WebAuthnLoginAssertionOptionsScope scope, AssertionOptions options) : this()
    {
        Scope = scope;
        Options = options;
    }

    public bool TokenIsValid(WebAuthnLoginAssertionOptionsScope scope)
    {
        if (!Valid)
        {
            return false;
        }

        return Scope == scope;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && Options != null;
}

