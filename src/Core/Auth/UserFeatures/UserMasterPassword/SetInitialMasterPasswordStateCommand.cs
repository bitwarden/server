using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class SetInitialMasterPasswordStateCommand : ISetInitialMasterPasswordStateCommand
{
    private readonly IUserRepository _userRepository;

    public SetInitialMasterPasswordStateCommand(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task ExecuteAsync(User user) => _userRepository.ReplaceAsync(user);
}
