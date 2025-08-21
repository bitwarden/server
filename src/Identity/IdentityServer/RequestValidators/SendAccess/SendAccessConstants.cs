using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public static class SendTokenAccessConstants
{
    /// <summary>
    /// Custom input on the request that is an URL encoded GUID used to look up the send <see cref="SendAccessGrantValidator"/>
    /// </summary>
    public const string SendId = "sendId";
    /// <summary>
    /// Custom input on the request that is a client base 64 hashed password <see cref="SendPasswordRequestValidator"/>
    /// </summary>
    public const string ClientBase64HashedPassword = "passwordHashB64";
    public const string SendAccessEmail = "email";
    public const string SendAccessEmailOtp = "otp";
    /// <summary>
    /// A catch all error type for send access related errors. Used mainly in the <see cref="GrantValidationResult.CustomResponse"/>
    /// </summary>
    public const string SendAccessError = "send_access_error_type";
}
