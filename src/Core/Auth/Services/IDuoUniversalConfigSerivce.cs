namespace Bit.Core.Auth.Services;

/// <summary>
/// In the TwoFactorController before we write a configuration to the database we check if the Duo configuration
/// is valid. This interface creates a simple way to inject the process into those endpoints.
/// </summary>
public interface IDuoUniversalConfigService
{
    /// <summary>
    /// Generates a Duo.Client object for use with Duo SDK v4. This method is to validate a Duo configuration
    /// when adding or updating the configuration. This method makes a web request to Duo to verify the configuration.
    /// Throws exception if configuration is invalid.
    /// </summary>
    /// <param name="clientSecret">Duo client Secret</param>
    /// <param name="clientId">Duo client Id</param>
    /// <param name="host">Duo host</param>
    /// <returns>Boolean</returns>
    Task<bool> ValidateDuoConfiguration(string clientSecret, string clientId, string host);
}
