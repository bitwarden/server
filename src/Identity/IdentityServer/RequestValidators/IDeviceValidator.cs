using Bit.Core.Entities;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public interface IDeviceValidator
{
    /// <summary>
    /// Check if requesting device is known to the user, if not, save the requesting device and send
    /// an email to the user to notify them of a new device login.
    /// </summary>
    /// <param name="user">The user is assumed NOT null, still going to check though</param>
    /// <param name="request">Duende Validated Request that contains the data to create the device object</param>
    /// <returns>Returns null if user or device is malformed, device if it already exists, or a new device</returns>
    [Obsolete("This method is being replaced when new-device-verification is complete")]
    Task<Device> SaveRequestingDeviceAsync(User user, ValidatedTokenRequest request);

    /// <summary>
    /// Fetches device from the database using the Device Identifier and the User Id to know if the user
    /// has ever tried to authenticate with this specific instance of Bitwarden.
    /// </summary>
    /// <param name="user">user attempting to authenticate</param>
    /// <param name="device">current instance of Bitwarden the user is interacting with</param>
    /// <returns>null or Device</returns>
    Task<Device> GetKnownDeviceAsync(User user, Device device);

    /// <summary>
    /// If the device is not known then save the device to the Database.
    /// </summary>
    /// <param name="user">User authenticating </param>
    /// <param name="device">request device</param>
    /// <returns>request device</returns>
    Task<Device> SaveRequestingDeviceAsync(User user, Device device);

    /// <summary>
    /// builds a response object to inform the client the instance of Bitwarden is new to the user
    /// authenticating and the user is required to verify the device by entering a valid email token.
    /// </summary>
    /// <param name="user">user attempting to authenticate</param>
    /// <param name="device">requesting device</param>
    /// <returns></returns>
    Task<(bool, string)> HandleNewDeviceVerificationAsync(User user, ValidatedRequest request);
}