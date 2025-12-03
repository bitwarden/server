using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.KeyManagement.Commands.Interfaces;

/// <summary>
/// Command to set account keys for a new user that does not have keys yet. This supports both V1 user and V2 user initialization.
/// This is intended for the TDE and Key-connector account registration flows.
/// </summary>
public interface ISetAccountKeysForUserCommand
{
    Task SetAccountKeysForUserAsync(Guid userId,
        AccountKeysRequestModel accountKeys);
}
