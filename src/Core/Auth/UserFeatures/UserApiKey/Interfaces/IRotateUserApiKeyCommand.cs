using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.UserApiKey.Interfaces;

public interface IRotateUserApiKeyCommand
{
    Task RotateApiKeyAsync(User user);
}
