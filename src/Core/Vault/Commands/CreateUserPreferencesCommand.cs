using Bit.Core.Exceptions;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands;

public class CreateUserPreferencesCommand(IUserPreferencesRepository userPreferencesRepository)
    : ICreateUserPreferencesCommand
{
    public async Task<UserPreferences> CreateAsync(Guid userId, string data)
    {
        var existing = await userPreferencesRepository.GetByUserIdAsync(userId);
        if (existing != null)
        {
            throw new BadRequestException("User preferences already exist.");
        }

        var preferences = UserPreferences.Create(userId, data);
        await userPreferencesRepository.CreateAsync(preferences);
        return preferences;
    }
}
