using Bit.Core.Entities;
using Bit.Core.KeyManagement.MasterPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.MasterPassword;

/// <inheritdoc />
public class SetInitialMasterPasswordCommand : ISetInitialMasterPasswordCommand
{
    private readonly ISetInitialMasterPasswordQuery _query;
    private readonly IUserRepository _userRepository;

    public SetInitialMasterPasswordCommand(ISetInitialMasterPasswordQuery query, IUserRepository userRepository)
    {
        _query = query;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task RunAsync(User user, SetInitialMasterPasswordData data)
    {
        await _query.RunAsync(user, data);
        await _userRepository.ReplaceAsync(user);
    }
}
