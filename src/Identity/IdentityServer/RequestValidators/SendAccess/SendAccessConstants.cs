using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

/// <summary>
/// String constants for the Send Access user feature
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
        /// The sendId is valid and the request is well formed.
        /// </summary>
        public const string ValidSendGuid = "valid_send_guid";
        /// <summary>
        /// The sendId is missing from the request.
        /// </summary>
        public const string MissingSendId = "send_id_required";
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
        public const string EmailOtpInvalid = "email_and_otp_required_otp_invalid";
        /// <summary>
        /// For what ever reason the OTP was not able to be generated
        /// </summary>
        public const string OtpGenerationFailed = "otp_generation_failed";
    }

    /// <summary>
    /// These are the constants for the OTP token that is generated during the email otp authentication process.
    /// </summary>
    public static class OtpToken
    {
        public const string Purpose = "send_access_email_otp";
        public const string TokenProviderName = "send_access_email_otp";
        /// <summary>
        /// This will be send_id {0} and email {1}
        /// </summary>
        public const string TokenUniqueIdentifier = "{0}_{1}";
    }
}
