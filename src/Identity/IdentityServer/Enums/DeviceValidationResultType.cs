namespace Bit.Identity.IdentityServer.Enums;

public enum DeviceValidationResultType : byte
{
    Success = 0,
    InvalidUser = 1,
    InvalidNewDeviceOtp = 2,
    NewDeviceVerificationRequired = 3,
    NoDeviceInformationProvided = 4
}
