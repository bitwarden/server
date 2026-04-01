using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface IUpdateUserPreferencesCommand
{
    /// <summary>
    /// Updates existing user preferences for the specified user.
    /// </summary>
    /// <param name="userId">The id of the user</param>
    /// <param name="data">The encrypted preferences data</param>
    /// <returns>The updated user preferences</returns>
    /// <exception cref="Exceptions.NotFoundException">Thrown when no preferences exist for the user</exception>
    Task<UserPreferences> UpdateAsync(Guid userId, string data);
}
