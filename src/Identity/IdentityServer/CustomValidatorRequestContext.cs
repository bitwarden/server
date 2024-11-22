using Bit.Core.Auth.Models.Business;
using Bit.Core.Entities;

namespace Bit.Identity.IdentityServer;

public class CustomValidatorRequestContext
{
    public User User { get; set; }
    /// <summary>
    /// Communicates whether or not the device in the request is known to the user.
    /// KnownDevice is set in the child classes of the BaseRequestValidator using the DeviceValidator.KnownDeviceAsync method.
    /// Except in the CustomTokenRequestValidator, where it is hardcoded to true.
    /// </summary>
    public bool KnownDevice { get; set;}
    /// <summary>
    /// This is the device that the user is using to authenticate. It can be either known or unknown.
    /// </summary>
    public Device Device { get; set; }
    public CaptchaResponse CaptchaResponse { get; set; }
}
