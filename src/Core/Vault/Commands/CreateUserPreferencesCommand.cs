using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands;

public class CreateUserPreferencesCommand(IUserPreferencesRepository userPreferencesRepository)
    : ICreateUserPreferencesCommand
{
    public async Task<UserPreferences> CreateAsync(Guid userId, string data)
    {
        var preferences = UserPreferences.Create(userId, data);
        await userPreferencesRepository.CreateAsync(preferences);
        return preferences;
    }
}
