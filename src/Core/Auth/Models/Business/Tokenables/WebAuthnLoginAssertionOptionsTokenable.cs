using System.Text.Json.Serialization;
using Bit.Core.Auth.Enums;
using Bit.Core.Tokens;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class WebAuthnLoginAssertionOptionsTokenable : ExpiringTokenable
{
    // Lifetime 17 minutes =
    //  - 6 Minutes for Attestation (max webauthn timeout)
    //  - 6 Minutes for PRF Assertion (max webauthn timeout)
    //  - 5 minutes for user to complete the process (name their passkey, etc)
    private static readonly TimeSpan _tokenLifetime = TimeSpan.FromMinutes(17);
    public const string ClearTextPrefix = "BWWebAuthnLoginAssertionOptions_";
    public const string DataProtectorPurpose = "WebAuthnLoginAssertionOptionsDataProtector";
    public const string TokenIdentifier = "WebAuthnLoginAssertionOptionsToken";

    public string Identifier { get; set; } = TokenIdentifier;
    public AssertionOptions Options { get; set; }
    public WebAuthnLoginAssertionOptionsScope Scope { get; set; }

    [JsonConstructor]
    public WebAuthnLoginAssertionOptionsTokenable()
    {
        ExpirationDate = DateTime.UtcNow.Add(_tokenLifetime);
    }

    public WebAuthnLoginAssertionOptionsTokenable(
        WebAuthnLoginAssertionOptionsScope scope,
        AssertionOptions options
    )
        : this()
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
