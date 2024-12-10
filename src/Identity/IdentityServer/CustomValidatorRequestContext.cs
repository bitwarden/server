using Bit.Core.Auth.Models.Business;
using Bit.Core.Entities;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer;

public class CustomValidatorRequestContext
{
    public User User { get; set; }
    /// <summary>
    /// This is the device that the user is using to authenticate. It can be either known or unknown.
    /// We set it here since the ResourceOwnerPasswordValidator needs the device to know if CAPTCHA is required.
    /// The option to set it here saves a trip to the database.
    /// </summary>
    public Device Device { get; set; }
    /// <summary>
    /// Communicates whether or not the device in the request is known to the user.
    /// KnownDevice is set in the child classes of the BaseRequestValidator using the DeviceValidator.KnownDeviceAsync method.
    /// Except in the CustomTokenRequestValidator, where it is hardcoded to true.
    /// </summary>
    public bool KnownDevice { get; set; }
    /// <summary>
    /// This communicates whether or not two factor is required for the user to authenticate.
    /// </summary>
    public bool TwoFactorRequired { get; set; } = false;
    /// <summary>
    /// This communicates whether or not SSO is required for the user to authenticate.
    /// </summary>
    public bool SsoRequired { get; set; } = false;
    /// <summary>
    /// We use the parent class for both GrantValidationResult and TokenRequestValidationResult here for
    /// flexibility when building the response.
    /// </summary>
    public ValidationResult ValidationErrorResult {get; set;}
    /// <summary>
    /// This dictionary should contain relevant information for the clients to act on.
    /// This contains the information used to guide a user to successful authentication.
    /// </summary>
    public Dictionary<string, object> CustomResponse { get; set; }
    public CaptchaResponse CaptchaResponse { get; set; }
}
