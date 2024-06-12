using Bit.Core.Entities;

namespace Bit.Commercial.Core.SecretsManager.Commands.PasswordManager;
public interface IRequestSMAccessCommand
{
    Task<bool> SendRequestAccessToSM(Guid organizationId, User user, string emailContent);
}
