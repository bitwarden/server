using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Commands;

public class SetUserKeyIdCommand : ISetUserKeyIdCommand
{
    private readonly IUserRepository _userRepository;

    public SetUserKeyIdCommand(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task SetUserKeyIdAsync(User user, KeyId userKeyId)
    {
        if (user.GetUserKeyId() is not null)
        {
            throw new BadRequestException("User key id is already set.");
        }

        await _userRepository.UpdateUserDataAsync([_userRepository.SetUserKeyId(user.Id, userKeyId)]);
    }
}
