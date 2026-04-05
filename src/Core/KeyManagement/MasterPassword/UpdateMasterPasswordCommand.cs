using Bit.Core.Entities;
using Bit.Core.KeyManagement.MasterPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.MasterPassword;

/// <inheritdoc />
public class UpdateMasterPasswordCommand : IUpdateMasterPasswordCommand
{
    private readonly IUpdateMasterPasswordQuery _query;
    private readonly IUserRepository _userRepository;

    public UpdateMasterPasswordCommand(IUpdateMasterPasswordQuery query, IUserRepository userRepository)
    {
        _query = query;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task RunAsync(User user, UpdateMasterPasswordData data)
    {
        await _query.RunAsync(user, data);
        await _userRepository.ReplaceAsync(user);
    }
}
