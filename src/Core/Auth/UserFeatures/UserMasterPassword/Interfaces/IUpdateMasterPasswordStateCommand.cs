using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

public interface IUpdateMasterPasswordStateCommand
{
    Task ExecuteAsync(User user);
}
