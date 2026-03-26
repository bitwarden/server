using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

public interface ISetInitialMasterPasswordStateCommand
{
    Task ExecuteAsync(User user);
}
