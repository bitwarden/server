namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess.Enums;

/// <summary>
/// These control the results of the SendPasswordValidator. <see cref="SendPasswordRequestValidator"/>
/// </summary>
internal enum SendPasswordValidatorResultTypes
{
    SendPasswordNullOrEmpty,
    RequestPasswordNullOrEmpty,
    RequestPasswordDoesNotMatch
}
