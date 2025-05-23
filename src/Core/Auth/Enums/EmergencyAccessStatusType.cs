namespace Bit.Core.Auth.Enums;

public enum EmergencyAccessStatusType : byte
{
    /// <summary>
    /// The user has been invited to be an emergency contact.
    /// </summary>
    Invited = 0,
    /// <summary>
    /// The invited user, "grantee", has accepted the request to be an emergency contact.
    /// </summary>
    Accepted = 1,
    /// <summary>
    /// The inviting user, "grantor", has approved the grantee's acceptance.
    /// </summary>
    Confirmed = 2,
    /// <summary>
    /// The grantee has initiated the recovery process.
    /// </summary>
    RecoveryInitiated = 3,
    /// <summary>
    /// The grantee has excercised their emergency access.
    /// </summary>
    RecoveryApproved = 4,
}
