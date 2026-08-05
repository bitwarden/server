using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class UpdateMasterPasswordSaltCommand : IUpdateMasterPasswordSaltCommand
{
    private readonly IUserRepository _userRepository;

    public UpdateMasterPasswordSaltCommand(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task UpdateAsync(User user)
    {
        // A cheap pre-filter, not the guard. It spares the vast majority of refreshes a database
        // round trip once the salt is populated; correctness is enforced by the conditional UPDATE.
        if (user.MasterPasswordSalt is not null || user.MasterPassword is null)
        {
            return;
        }

        await _userRepository.SetMasterPasswordSaltIfNullAsync(user.Id, user.Email.ToLowerInvariant().Trim());
    }
}
