using Bit.Core.Exceptions;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands;

public class UpdateUserPreferencesCommand(IUserPreferencesRepository userPreferencesRepository)
    : IUpdateUserPreferencesCommand
{
    public async Task<UserPreferences> UpdateAsync(Guid userId, string data)
    {
        var existing = await userPreferencesRepository.GetByUserIdAsync(userId);
        if (existing == null)
        {
            throw new NotFoundException();
        }

        existing.Update(data);
        await userPreferencesRepository.ReplaceAsync(existing);
        return existing;
    }
}
