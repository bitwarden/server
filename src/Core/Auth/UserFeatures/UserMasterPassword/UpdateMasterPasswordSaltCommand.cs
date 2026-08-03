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

        // The salt is null here, so this returns the email-derived salt clients already use. The
        // revision dates are deliberately left alone: the stored value matches what clients compute,
        // so there is nothing for them to re-sync.
        user.MasterPasswordSalt = user.Email.ToLowerInvariant().Trim();
        await _userRepository.ReplaceAsync(user);
    }
}
