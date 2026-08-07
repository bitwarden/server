using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class UpdateMasterPasswordSaltCommand : IUpdateMasterPasswordSaltCommand
{
    private readonly IUserRepository _userRepository;

    public UpdateMasterPasswordSaltCommand(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task UpdateAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || user.MasterPasswordSalt is not null || user.MasterPassword is null)
        {
            return;
        }

        user.MasterPasswordSalt = user.Email.ToLowerInvariant().Trim();
        await _userRepository.ReplaceAsync(user);
    }
}
