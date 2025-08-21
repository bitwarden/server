namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess.Enums;

/// <summary>
/// These control the results of the SendPasswordValidator. <see cref="SendPasswordRequestValidator"/>
/// </summary>
internal static class SendPasswordValidatorResult
{
    // InvalidGrant is the correct shape, passwordHashB64 does not match.
    public static string RequestPasswordDoesNotMatch = "password_hash_b64_invalid";
    // InvalidRequest is the wrong shape, passwordHashB64 is missing.
    public static string RequestPasswordIsRequired = "password_hash_b64_required";
}
