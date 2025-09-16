using Bit.Core.Auth.Identity.TokenProviders;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

/// <summary>
/// String constants for the Send Access user feature
/// Most of these need to be synced with the `bitwarden-auth` crate in the SDK.
/// There is snapshot testing to help ensure this.
/// </summary>
public static class SendAccessConstants
{
    /// <summary>
    /// A catch all error type for send access related errors. Used mainly in the <see cref="GrantValidationResult.CustomResponse"/>
    /// </summary>
    public const string SendAccessError = "send_access_error_type";
    public static class TokenRequest
    {
        /// <summary>
        /// used to fetch Send from database.
        /// </summary>
        public const string SendId = "send_id";
        /// <summary>
        /// used to validate Send protected passwords
        /// </summary>
        public const string ClientB64HashedPassword = "password_hash_b64";
        /// <summary>
        /// email used to see if email is associated with the Send
        /// </summary>
        public const string Email = "email";
        /// <summary>
        /// Otp code sent to email associated with the Send
        /// </summary>
        public const string Otp = "otp";
    }

    public static class GrantValidatorResults
    {
        /// <summary>
        /// The sendId is valid and the request is well formed. Not returned in any response.
        /// </summary>
        public const string ValidGuid = "valid_send_guid";
        /// <summary>
        /// The sendId is missing from the request.
        /// </summary>
        public const string SendIdRequired = "send_id_required";
        /// <summary>
        /// The sendId is invalid, does not match a known send.
        /// </summary>
        public const string InvalidSendId = "send_id_invalid";
    }

    public static class PasswordValidatorResults
    {
        /// <summary>
        /// The passwordHashB64 does not match the send's password hash.
        /// </summary>
        public const string RequestPasswordDoesNotMatch = "password_hash_b64_invalid";
        /// <summary>
        /// The passwordHashB64 is missing from the request.
        /// </summary>
        public const string RequestPasswordIsRequired = "password_hash_b64_required";
    }

    public static class EmailOtpValidatorResults
    {
        /// <summary>
        /// Represents the error code indicating that an email address is required.
        /// </summary>
        public const string EmailRequired = "email_required";
        /// <summary>
        /// Represents the error code indicating that an email address is invalid.
        /// </summary>
        public const string EmailInvalid = "email_invalid";
        /// <summary>
        /// Represents the status indicating that both email and OTP are required, and the OTP has been sent.
        /// </summary>
        public const string EmailOtpSent = "email_and_otp_required_otp_sent";
        /// <summary>
        /// Represents the status indicating that both email and OTP are required, and the OTP is invalid.
        /// </summary>
        public const string EmailOtpInvalid = "otp_invalid";
        /// <summary>
        /// For what ever reason the OTP was not able to be generated
        /// </summary>
        public const string OtpGenerationFailed = "otp_generation_failed";
    }

    /// <summary>
    /// These are the constants for the OTP token that is generated during the email otp authentication process.
    /// These items are required by <see cref="IOtpTokenProvider{TOptions}"/> to aid in the creation of a unique lookup key.
    /// Look up key format is: {TokenProviderName}_{Purpose}_{TokenUniqueIdentifier}
    /// </summary>
    public static class OtpToken
    {
        public const string TokenProviderName = "send_access";
        public const string Purpose = "email_otp";
        /// <summary>
        /// This will be send_id {0} and email {1}
        /// </summary>
        public const string TokenUniqueIdentifier = "{0}_{1}";
    }

    public static class OtpEmail
    {
        public const string Subject = "Your Bitwarden Send verification code is {0}";
    }

    /// <summary>
    /// We use these static strings to help guide the enumeration protection logic.
    /// </summary>
    public static class EnumerationProtection
    {
        public const string Guid = "guid";
        public const string Password = "password";
        public const string Email = "email";
    }
}
