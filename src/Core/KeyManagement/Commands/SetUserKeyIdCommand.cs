using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Commands;

public class SetUserKeyIdCommand : ISetUserKeyIdCommand
{
    private readonly IUserRepository _userRepository;

    public SetUserKeyIdCommand(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task SetUserKeyIdAsync(Guid userId, Models.Data.KeyId userKeyId)
    {
        // The repository performs the "not already set" check and the write as one conditional
        // statement, so two clients racing to backfill cannot both succeed.
        var stored = await _userRepository.TrySetUserKeyIdAsync(userId, userKeyId);
        if (!stored)
        {
            throw new BadRequestException("User key id is already set.");
        }
    }
}
