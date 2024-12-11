namespace Bit.Core.Auth.Enums;

public enum WebAuthnLoginAssertionOptionsScope
{
    /*
        Authentication is used when a user is trying to login in with a credential.
    */
    Authentication = 0,

    /*
        PrfRegistration is used when a user is trying to register a new credential.
    */
    PrfRegistration = 1,

    /*
        UpdateKeySet is used when a user is enabling a credential for passwordless login
        This is done by adding rotatable keys to the credential.
    */
    UpdateKeySet = 2,
}
