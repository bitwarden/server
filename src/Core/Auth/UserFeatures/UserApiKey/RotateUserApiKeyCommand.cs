using Bit.Core.Auth.UserFeatures.UserApiKey.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.UserFeatures.UserApiKey;

public class RotateUserApiKeyCommand : IRotateUserApiKeyCommand
{
    private readonly IUserRepository _userRepository;

    public RotateUserApiKeyCommand(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task RotateApiKeyAsync(User user)
    {
        var now = DateTime.UtcNow;
        user.ApiKey = CoreHelpers.SecureRandomString(30);
        user.RevisionDate = now;
        user.LastApiKeyRotationDate = now;
        await _userRepository.ReplaceAsync(user);
    }
}
