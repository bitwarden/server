namespace Bit.Core.Auth.Enums;

/**
 * The type of auth request.
 *
 * Note:
 * Used by the Device_ReadActiveWithPendingAuthRequestsByUserId.sql stored procedure.
 *  If the enum changes be aware of this reference.
 */
public enum AuthRequestType : byte
{
    AuthenticateAndUnlock = 0,
    Unlock = 1,
    AdminApproval = 2,
}
