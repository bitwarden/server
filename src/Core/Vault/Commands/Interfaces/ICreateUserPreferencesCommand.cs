using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface ICreateUserPreferencesCommand
{
    /// <summary>
    /// Creates user preferences for the specified user.
    /// </summary>
    /// <param name="userId">The id of the user</param>
    /// <param name="data">The encrypted preferences data</param>
    /// <returns>The created user preferences</returns>
    Task<UserPreferences> CreateAsync(Guid userId, string data);
}
