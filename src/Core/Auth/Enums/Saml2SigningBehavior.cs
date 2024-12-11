namespace Bit.Core.Auth.Enums;

public enum Saml2SigningBehavior : byte
{
    IfIdpWantAuthnRequestsSigned = 0,
    Always = 1,
    Never = 3,
}
