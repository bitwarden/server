using Bit.Core.Entities;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public interface IDeviceValidator
{
    /// <summary>
    /// Fetches device from the database using the Device Identifier and the User Id to know if the user
    /// has ever tried to authenticate with this specific instance of Bitwarden.
    /// </summary>
    /// <param name="user">user attempting to authenticate</param>
    /// <param name="device">current instance of Bitwarden the user is interacting with</param>
    /// <returns>null or Device</returns>
    Task<Device> GetKnownDeviceAsync(User user, Device device);

    /// <summary>
    /// Validate the requesting device. Modifies the ValidatorRequestContext with error result if any.
    /// </summary>
    /// <param name="request">The Request is used to check for the NewDeviceOtp and for the raw device data</param>
    /// <param name="context">Contains two factor and sso context that are important for decisions on new device verification</param>
    /// <returns>returns true if device is valid and no other action required; if false modifies the context with an error result to be returned;</returns>
    Task<bool> ValidateRequestDeviceAsync(ValidatedTokenRequest request, CustomValidatorRequestContext context);
}
