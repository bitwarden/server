namespace Bit.Identity.IdentityServer.Enums;

public enum DeviceValidationErrorType : byte
{
    None = 0,
    InvalidUserOrDevice = 1,
    InvalidNewDeviceOtp = 2,
    NewDeviceVerificationRequired = 3,
    NoDeviceInformationProvided = 4
}