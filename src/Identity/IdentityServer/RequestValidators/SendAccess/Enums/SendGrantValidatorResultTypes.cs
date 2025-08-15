namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess.Enums;

/// <summary>
/// These control the results of the SendGrantValidator. <see cref="SendGrantValidator"/>
/// </summary>
internal enum SendGrantValidatorResultTypes
{
    ValidSendGuid,
    MissingSendId,
    InvalidSendId
}
