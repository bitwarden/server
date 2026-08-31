namespace Bit.Core.Enums;

public enum EventSystemUser : byte
{
    Unknown = 0,
    SCIM = 1,
    DomainVerification = 2,
    PublicApi = 3,
    TwoFactorDisabled = 4,
    BitwardenPortal = 5,

    /// <summary>PAM itself: an automatic access decision, or a background sweep acting on a lease.</summary>
    Pam = 6,
}
