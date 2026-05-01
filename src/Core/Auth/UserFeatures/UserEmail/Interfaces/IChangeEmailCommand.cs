using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.UserEmail.Interfaces;

public interface IChangeEmailCommand
{
    Task ChangeEmailAsync(User user, string newEmail);
}
